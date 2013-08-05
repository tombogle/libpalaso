﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Ionic.Zip;
using L10NSharp;
using Palaso.Reporting;
using Palaso.UI.WindowsForms;
using Palaso.IO;
using Palaso.UI.WindowsForms.Miscellaneous;
using Palaso.UI.WindowsForms.Progress;
using SIL.Archiving.Properties;
using Timer = System.Threading.Timer;

namespace SIL.Archiving
{
	public class ArchivingDlgViewModel
	{
#if !__MonoCS__
		[DllImport("User32.dll")]
		private static extern IntPtr SetForegroundWindow(int hWnd);

		[DllImport("User32.dll")]
		private static extern bool BringWindowToTop(int hWnd);
#endif

		private readonly string _title;
		private readonly string _id;
		private readonly Func<IEnumerable<string>> _getAppSpecificMetsPairs;
		private readonly Func<string, string, string> _getFileDescription; // first param is filelist key, second param is filename
		private readonly Func<ArchivingDlgViewModel, string, string, bool> _specialFileCopy; // first paramm is the source file path, second param is destination file path
		private readonly Action<string, string, StringBuilder> _appSpecificFilenameNormalization;
		private string _metsFilePath;
		private string _tempFolder;
		private BackgroundWorker _worker;
		private Timer _timer;
		private bool _cancelProcess;
		private bool _workerException;
		private readonly Dictionary<string, string> _progressMessages = new Dictionary<string, string>();
		private string _rampProgramPath;
		private Action _incrementProgressBarAction;
		private IDictionary<string, IEnumerable<string>> _fileLists;
		private readonly Font _programDialogFont;

		public bool IsBusy { get; private set; }
		public string RampPackagePath { get; private set; }
		public LogBox LogBox { get; private set; }
		internal Font ProgramDialogFont
		{
			get { return _programDialogFont; }
		}

		#region construction and initialization

		/// ------------------------------------------------------------------------------------
		/// <summary>Constructor</summary>
		/// <param name="title">Title of the submission</param>
		/// <param name="id">Identifier (used as filename) for the package being created</param>
		/// <param name="programDialogFont">The default dialog font used by the calling
		/// application to ensure a consistent look in the UI</param>
		/// <param name="getAppSpecificMetsPairs">Callback function for the application to
		/// supply application-specific strings to be included in METS file. These need to be
		/// formatted correctly as JSON key-value pairs.</param>
		/// <param name="getFileDescription">Callback function to get a file description based
		/// on the file-list key (param 1) and the filename (param 2)</param>
		/// <param name="specialFileCopy">Callback function to allow the calling application to
		/// modify the contents of a file rather than merely copying it. If application handles
		/// the "copy" it should return true; otherwise, false.</param>
		/// <param name="appSpecificFilenameNormalization">Callback to do application-specific
		/// normalization of filenames to be added to archive based on the file-list key
		/// (param 1) and the filename (param 2). The StringBuilder (param 3) has the normalized
		/// name which the app can further alter as needed.</param>
		/// ------------------------------------------------------------------------------------
		public ArchivingDlgViewModel(string title, string id, Font programDialogFont,
			Func<IEnumerable<string>> getAppSpecificMetsPairs, Func<string, string, string> getFileDescription,
			Func<ArchivingDlgViewModel, string, string, bool> specialFileCopy,
			Action<string, string, StringBuilder> appSpecificFilenameNormalization)
		{
			_title = title;
			_id = id;
			_getAppSpecificMetsPairs = getAppSpecificMetsPairs;
			if (getFileDescription == null)
				throw new ArgumentNullException("getFileDescription");
			_getFileDescription = getFileDescription;
			_specialFileCopy = specialFileCopy;
			_appSpecificFilenameNormalization = appSpecificFilenameNormalization;
			_programDialogFont = programDialogFont;

			LogBox = new LogBox();
			LogBox.TabStop = false;
			LogBox.ShowMenu = false;
			if (programDialogFont != null)
				LogBox.Font = FontHelper.MakeFont(programDialogFont, FontStyle.Bold);

			foreach (var orphanedRampPackage in Directory.GetFiles(Path.GetTempPath(), "*.ramp"))
			{
				try { File.Delete(orphanedRampPackage); }
				catch { }
			}
		}

		/// ------------------------------------------------------------------------------------
		public bool Initialize(Func<IDictionary<string, IEnumerable<string>>> getFilesToArchive,
			out int maxProgBarValue, Action incrementProgressBarAction)
		{
			IsBusy = true;
			_incrementProgressBarAction = incrementProgressBarAction;

			var text = LocalizationManager.GetString("DialogBoxes.ArchivingDlg.SearchingForRampMsg",
				"Searching for the RAMP program...");

			LogBox.WriteMessage(text);
			Application.DoEvents();
			_rampProgramPath = FileLocator.GetFromRegistryProgramThatOpensFileType(".ramp") ??
				FileLocator.LocateInProgramFiles("ramp.exe", true, "ramp");

			LogBox.Clear();

			if (_rampProgramPath == null)
			{
				text = LocalizationManager.GetString("DialogBoxes.ArchivingDlg.RampNotFoundMsg",
					"The RAMP program cannot be found!");

				LogBox.WriteMessageWithColor("Red", text + Environment.NewLine);
			}

			_fileLists = getFilesToArchive();
			DisplayInitialSummary();
			IsBusy = false;

			// One for analyzing each list, one for copying each file, one for saving each file in the zip file
			// and one for the mets.xml file.
			maxProgBarValue = _fileLists.Count + 2 * _fileLists.SelectMany(kvp => kvp.Value).Count() + 1;

			return (_rampProgramPath != null);
		}

		/// ------------------------------------------------------------------------------------
		private void DisplayInitialSummary()
		{
			if (_fileLists.Count > 1)
			{
				LogBox.WriteMessage(LocalizationManager.GetString("DialogBoxes.ArchivingDlg.PrearchivingStatusMsg1",
					"The following session and contributor files will be added to your archive."));
			}
			else
			{
				LogBox.WriteWarning(LocalizationManager.GetString("DialogBoxes.ArchivingDlg.NoContributorsForSessionMsg",
					"There are no contributors for this session."));

				LogBox.WriteMessage(Environment.NewLine +
					LocalizationManager.GetString("DialogBoxes.ArchivingDlg.PrearchivingStatusMsg2",
						"The following session files will be added to your archive."));
			}

			var fmt = LocalizationManager.GetString("DialogBoxes.ArchivingDlg.ArchivingProgressMsg", "     {0}: {1}",
				"The first parameter is 'Session' or 'Contributor'. The second parameter is the session or contributor name.");

			foreach (var kvp in _fileLists)
			{
				var element = (kvp.Key == string.Empty ?
					LocalizationManager.GetString("DialogBoxes.ArchivingDlg.SessionElementName", "Session") :
					LocalizationManager.GetString("DialogBoxes.ArchivingDlg.ContributorElementName", "Contributor"));

				LogBox.WriteMessage(Environment.NewLine + string.Format(fmt, element,
					(kvp.Key == string.Empty ? _title : kvp.Key)));

				foreach (var file in kvp.Value)
					LogBox.WriteMessageWithFontStyle(FontStyle.Regular, "          \u00B7 {0}", Path.GetFileName(file));
			}

			LogBox.ScrollToTop();
		}

		#endregion

		#region RAMP calling methods
		/// ------------------------------------------------------------------------------------
		public bool CallRAMP()
		{
			if (!File.Exists(RampPackagePath))
			{
				ErrorReport.NotifyUserOfProblem("Eeeek. SayMore prematurely removed .ramp package.");
				return false;
			}

			try
			{
				var prs = new Process();
				prs.StartInfo.FileName = _rampProgramPath;
				prs.StartInfo.Arguments = "\"" + RampPackagePath + "\"";
				if (!prs.Start())
					return false;

				prs.WaitForInputIdle(8000);
				EnsureRampHasFocusAndWaitForPackageToUnlock();
				return true;
			}
			catch (InvalidOperationException)
			{
				EnsureRampHasFocusAndWaitForPackageToUnlock();
				return true;
			}
			catch (Exception e)
			{
				ReportError(e, LocalizationManager.GetString("DialogBoxes.ArchivingDlg.StartingRampErrorMsg",
					"There was an error attempting to open the archive package in RAMP."));

				return false;
			}
		}

		/// ------------------------------------------------------------------------------------
		private void EnsureRampHasFocusAndWaitForPackageToUnlock()
		{
#if !__MonoCS__
			var processes = Process.GetProcessesByName("RAMP");
			if (processes.Length >= 1)
			{
				// I can't figure out why neither of these work.
				BringWindowToTop(processes[0].MainWindowHandle.ToInt32());
//				SetForegroundWindow(processes[0].MainWindowHandle.ToInt32());
			}
#else
			// Figure out how to do this in MONO
#endif
			// Every 4 seconds we'll check to see if the RAMP package is locked. When
			// it gets unlocked by RAMP, then we'll delete it.
			_timer = new Timer(CheckIfPackageFileIsLocked, RampPackagePath, 2000, 4000);
		}

		/// ------------------------------------------------------------------------------------
		private void CheckIfPackageFileIsLocked(Object packageFile)
		{
			if (!FileUtils.IsFileLocked(packageFile as string))
				CleanUpTempRampPackage();
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		public bool CreatePackage()
		{
			IsBusy = true;
			LogBox.Clear();

			var	success = CreateMetsFile() != null;

			if (success)
				success = CreateRampPackage();

			CleanUp();

			if (success)
			{
				LogBox.WriteMessageWithColor(Color.DarkGreen, Environment.NewLine +
					LocalizationManager.GetString("DialogBoxes.ArchivingDlg.ReadyToCallRampMsg",
					"Ready to hand the package to RAMP"));
			}

			IsBusy = false;
			return success;
		}

		#region Methods for creating mets file.
		/// ------------------------------------------------------------------------------------
		public string CreateMetsFile()
		{
			try
			{
				var bldr = new StringBuilder();

				foreach (var value in GetMetsPairs())
					bldr.AppendFormat("{0},", value);

				var jsonData = string.Format("{{{0}}}", bldr.ToString().TrimEnd(','));
				jsonData = JSONUtils.EncodeData(jsonData);
				var metsData = Resources.EmptyMets.Replace("<binData>", "<binData>" + jsonData);
				_tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
				Directory.CreateDirectory(_tempFolder);
				_metsFilePath = Path.Combine(_tempFolder, "mets.xml");
				File.WriteAllText(_metsFilePath, metsData);
			}
			catch (Exception e)
			{
				if ((e is IOException) || (e is UnauthorizedAccessException) || (e is SecurityException))
				{
					ReportError(e, LocalizationManager.GetString("DialogBoxes.ArchivingDlg.CreatingInternalReapMetsFileErrorMsg",
						"There was an error attempting to create a RAMP/REAP mets file for the session '{0}'."));
					return null;
				}
				throw;
			}

			if (_incrementProgressBarAction != null)
				_incrementProgressBarAction();

			return _metsFilePath;
		}

		/// ------------------------------------------------------------------------------------
		public IEnumerable<string> GetMetsPairs()
		{
			yield return JSONUtils.MakeKeyValuePair("dc.title", _title);

			if (_getAppSpecificMetsPairs != null)
			{
				foreach (string appSpecificPair in _getAppSpecificMetsPairs())
					yield return appSpecificPair;
			}

			if (_fileLists != null)
			{
				// Return a list of types found in session's files (e.g. Text, Video, etc.).
				string value = GetMode(_fileLists.SelectMany(f => f.Value));
				if (value != null)
					yield return value;

				// Return JSON array of session and contributor files with their descriptions.
				yield return JSONUtils.MakeArrayFromValues("files",
					GetSourceFilesForMetsData(_fileLists));
			}
		}

		/// ------------------------------------------------------------------------------------
		public string GetMode(IEnumerable<string> files)
		{
			if (files == null)
				return null;

			var list = new HashSet<string>();

			foreach (var file in files)
			{
				if (FileUtils.GetIsAudio(file))
					list.Add("Speech");
				if (FileUtils.GetIsVideo(file))
					list.Add("Video");
				if (FileUtils.GetIsText(file))
					list.Add("Text");
				if (FileUtils.GetIsImage(file))
					list.Add("Photograph");
			}

			return JSONUtils.MakeBracketedListFromValues("dc.type.mode", list);
		}

		/// ------------------------------------------------------------------------------------
		public IEnumerable<string> GetSourceFilesForMetsData(IDictionary<string, IEnumerable<string>> fileLists)
		{
			foreach (var kvp in fileLists)
			{
				foreach (var file in kvp.Value)
				{
					var description = _getFileDescription(kvp.Key, file);

					var fileName = NormalizeFilenameForRAMP(kvp.Key, Path.GetFileName(file));

					yield return JSONUtils.MakeKeyValuePair(" ", fileName) + "," +
						JSONUtils.MakeKeyValuePair("description", description) + "," +
						JSONUtils.MakeKeyValuePair("relationship", "source");
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		public string NormalizeFilenameForRAMP(string key, string fileName)
		{
			StringBuilder bldr = new StringBuilder(fileName);
			int prevPeriod = -1;
			for (int i = 0; i < bldr.Length; i++)
			{
				if (bldr[i] == ' ')
					bldr[i] = '+';
				else if (bldr[i] == '.')
				{
					if (prevPeriod >= 0)
						bldr[prevPeriod] = '#';
					prevPeriod = i;
				}
			}
			if (_appSpecificFilenameNormalization != null)
				_appSpecificFilenameNormalization(key, fileName, bldr);
			return bldr.ToString();
		}
		#endregion

		#region Creating RAMP package (zip file) in background thread.
		/// ------------------------------------------------------------------------------------
		public bool CreateRampPackage()
		{
			try
			{
				RampPackagePath = Path.Combine(Path.GetTempPath(), _id + ".ramp");

				using (_worker = new BackgroundWorker())
				{
					_cancelProcess = false;
					_workerException = false;
					_worker.ProgressChanged += HandleBackgroundWorkerProgressChanged;
					_worker.WorkerReportsProgress = true;
					_worker.WorkerSupportsCancellation = true;
					_worker.DoWork += CreateZipFileInWorkerThread;
					_worker.RunWorkerAsync();

					while (_worker.IsBusy)
						Application.DoEvents();
				}
			}
			catch (Exception e)
			{
				ReportError(e, LocalizationManager.GetString(
					"DialogBoxes.ArchivingDlg.CreatingZipFileErrorMsg",
					"There was a problem starting process to create zip file."));

				return false;
			}
			finally
			{
				_worker = null;
			}

			if (!File.Exists(RampPackagePath))
			{
				ErrorReport.NotifyUserOfProblem("Ack. SayMore failed to actually make the .ramp package.");
				return false;
			}

			return !_cancelProcess && !_workerException;
		}

		/// ------------------------------------------------------------------------------------
		private void CreateZipFileInWorkerThread(object sender, DoWorkEventArgs e)
		{
			try
			{
				// Before adding the files to the RAMP (zip) file, we need to copy all the
				// files to a temp folder, flattening out the directory structure and renaming
				// the files as needed to comply with REAP guidelines.
				// REVIEW: Are multiple periods and/or non-Roman script really a problem?

				_worker.ReportProgress(0, LocalizationManager.GetString("DialogBoxes.ArchivingDlg.PreparingFilesMsg",
					"Analyzing component files"));

				var filesToCopyAndZip = new Dictionary<string, string>();
				foreach (var list in _fileLists)
				{
					_worker.ReportProgress(1 /* actual value ignored, progress just increments */,
						string.IsNullOrEmpty(list.Key) ? _id: list.Key);
					foreach (var file in list.Value)
					{
						string newFileName = Path.GetFileName(file);
						newFileName = NormalizeFilenameForRAMP(list.Key, newFileName);
						filesToCopyAndZip[file] = Path.Combine(_tempFolder, newFileName);
					}
					if (_cancelProcess)
						return;
				}

				_worker.ReportProgress(0, LocalizationManager.GetString("DialogBoxes.ArchivingDlg.CopyingFilesMsg",
					"Copying files"));

				foreach (var fileToCopy in filesToCopyAndZip)
				{
					if (_cancelProcess)
						return;
					_worker.ReportProgress(1 /* actual value ignored, progress just increments */,
						Path.GetFileName(fileToCopy.Key));
					if (_specialFileCopy != null)
					{
						try
						{
							if (_specialFileCopy(this, fileToCopy.Key, fileToCopy.Value))
							{
								if (!File.Exists(fileToCopy.Value))
									throw new FileNotFoundException("Calling application claimed to copy file but didn't", fileToCopy.Value);
								continue;
							}
						}
						catch (Exception error)
						{
							ErrorReport.NotifyUserOfProblem(error, LocalizationManager.GetString(
								"DialogBoxes.ArchivingDlg.FileExcludedFromRAMP", "File excluded from RAMP package."));
						}
					}
					// Don't use File.Copy because it's asynchronous.
					CopyFile(fileToCopy.Key, fileToCopy.Value);
				}

				_worker.ReportProgress(0, LocalizationManager.GetString("DialogBoxes.ArchivingDlg.SavingFilesInRAMPMsg",
					"Saving files in RAMP package"));

				using (var zip = new ZipFile())
				{
					// RAMP packages must not be compressed or RAMP can't read them.
					zip.CompressionLevel = Ionic.Zlib.CompressionLevel.None;
					zip.AddFiles(filesToCopyAndZip.Values, @"\");
					zip.AddFile(_metsFilePath, string.Empty);
					zip.SaveProgress += HandleZipSaveProgress;
					zip.Save(RampPackagePath);

					if (!_cancelProcess && _incrementProgressBarAction != null)
						Thread.Sleep(800);
				}
			}
			catch (Exception exception)
			{
				_worker.ReportProgress(0, new KeyValuePair<Exception, string>(exception,
					LocalizationManager.GetString("DialogBoxes.ArchivingDlg.CreatingArchiveErrorMsg",
						"There was an error attempting to create an archive for the session '{0}'.")));

				_workerException = true;
			}
		}

		/// ------------------------------------------------------------------------------------
		const int CopyBufferSize = 64 * 1024;
		static void CopyFile(string src, string dest)
		{
			using (var outputFile = File.OpenWrite(dest))
			{
				using (var inputFile = File.OpenRead(src))
				{
					var buffer = new byte[CopyBufferSize];
					int bytesRead;
					while ((bytesRead = inputFile.Read(buffer, 0, CopyBufferSize)) != 0)
					{
						outputFile.Write(buffer, 0, bytesRead);
					}
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This is called by the Save method on the ZipFile class as the zip file is being
		/// saved to the disk.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void HandleZipSaveProgress(object s, SaveProgressEventArgs e)
		{
			if (_cancelProcess || e.EventType != ZipProgressEventType.Saving_BeforeWriteEntry)
				return;

			string msg;
			if (_progressMessages.TryGetValue(e.CurrentEntry.FileName, out msg))
				LogBox.WriteMessage(Environment.NewLine + msg);

			_worker.ReportProgress(e.EntriesSaved + 1, Path.GetFileName(e.CurrentEntry.FileName));
		}

		/// ------------------------------------------------------------------------------------
		void HandleBackgroundWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (e.UserState == null || _cancelProcess)
				return;

			if (e.UserState is KeyValuePair<Exception, string>)
			{
				var kvp = (KeyValuePair<Exception, string>)e.UserState;
				ReportError(kvp.Key, kvp.Value);
				return;
			}

			if (!string.IsNullOrEmpty(e.UserState as string))
			{
				if (e.ProgressPercentage == 0)
				{
					LogBox.WriteMessageWithColor(Color.DarkGreen, Environment.NewLine + e.UserState);
					return;
				}

				LogBox.WriteMessageWithFontStyle(FontStyle.Regular, "\t" + e.UserState);
			}

			if (!_cancelProcess && _incrementProgressBarAction != null)
				_incrementProgressBarAction();
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		public void Cancel()
		{
			if (_cancelProcess)
				return;

			_cancelProcess = true;

			if (_worker != null)
			{
				LogBox.WriteMessageWithColor(Color.Red, Environment.NewLine +
					LocalizationManager.GetString("DialogBoxes.ArchivingDlg.CancellingMsg", "Canceling..."));

				_worker.CancelAsync();
				while (_worker.IsBusy)
					Application.DoEvents();
			}

			CleanUp();
			CleanUpTempRampPackage();
		}

		/// ------------------------------------------------------------------------------------
		private void ReportError(Exception e, string msg)
		{
			if (!LogBox.IsHandleCreated)
			{
				WaitCursor.Hide();
				LogBox.WriteError(msg, _title);
				LogBox.WriteException(e);
			}
			else
			{
				throw e;
			}
		}

		#region Clean-up methods
		/// ------------------------------------------------------------------------------------
		public void CleanUp()
		{
			try { Directory.Delete(_tempFolder, true); }
			catch { }
		}

		/// ------------------------------------------------------------------------------------
		public void CleanUpTempRampPackage()
		{
			// Comment out as a test !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
			//try { File.Delete(RampPackagePath); }
			//catch { }

			if (_timer != null)
			{
				_timer.Dispose();
				_timer = null;
			}
		}

		#endregion
	}
}
﻿using System.IO;
using System.Xml.Linq;
using SIL.Code;

namespace SIL.IO
{
	/// <summary>
	/// Provides a more robust version of various IO methods.
	/// The original intent of this class is to attempt to mitigate issues
	/// where we attempt IO but the file is locked by another application.
	/// Our theory is that some anti-virus software locks files while it scans them.
	/// </summary>
	public static class RobustIO
	{
		public static void DeleteDirectory(string path)
		{
			// This is not the same as DirectoryUtilities.DeleteDirectoryRobust(path)
			// as this version requires the directory to be empty.
			RetryUtility.Retry(() => Directory.Delete(path));
		}

		public static void DeleteDirectory(string path, bool recursive)
		{
			if(recursive)
			{
				var succeeded = DirectoryUtilities.DeleteDirectoryRobust(path);
				if(!succeeded)
					throw new IOException("Could not delete directory "+path);
			}
			else
				RetryUtility.Retry(() => Directory.Delete(path, false));
		}

		public static void MoveDirectory(string sourceDirName, string destDirName)
		{
			RetryUtility.Retry(() => Directory.Move(sourceDirName, destDirName));
		}
		
		public static XElement LoadXElement(string uri)
		{
			return RetryUtility.Retry(() => XElement.Load(uri));
		}

		public static void SaveXElement(XElement xElement, string fileName)
		{
			RetryUtility.Retry(() => xElement.Save(fileName));
		}
	}
}

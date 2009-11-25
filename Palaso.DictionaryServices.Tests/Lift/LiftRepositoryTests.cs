using System.IO;
using System.Xml;
using NUnit.Framework;
using Palaso.DictionaryServices.Lift;
using Palaso.DictionaryServices.Model;
using Palaso.Lift.Options;
using Palaso.Progress;
using Palaso.Tests.Data;
using Palaso.TestUtilities;

namespace Palaso.DictionaryServices.Tests.Lift
{
	internal static class LiftFileInitializer
	{
		public static string MakeFile(string liftFileName)
		{
			File.WriteAllText(liftFileName,
							  @"<?xml version='1.0' encoding='utf-8'?>
				<lift
					version='0.13'
					producer='WeSay 1.0.0.0'>
					<entry
						id='Sonne_c753f6cc-e07c-4bb1-9e3c-013d09629111'
						dateCreated='2008-07-01T06:29:23Z'
						dateModified='2008-07-01T06:29:57Z'
						guid='c753f6cc-e07c-4bb1-9e3c-013d09629111'>
						<lexical-unit>
							<form
								lang='v'>
								<text>Sonne</text>
							</form>
						</lexical-unit>
						<sense
							id='33d60091-ba96-4204-85fe-9d15a24bd5ff'>
							<trait
								name='SemanticDomainDdp4'
								value='1 Universe, creation' />
						</sense>
					</entry>
				</lift>");
			return liftFileName;
		}
	}

	[TestFixture]
	public class LiftRepositoryStateUnitializedTests: IRepositoryStateUnitializedTests<LexEntry>
	{
		private string _persistedFilePath;
		private TemporaryFolder _tempFolder;

		[SetUp]
		public override void SetUp()
		{
			_tempFolder = new TemporaryFolder();
			_persistedFilePath = _tempFolder.GetTemporaryFile();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		[TearDown]
		public override void TearDown()
		{
			DataMapperUnderTest.Dispose();
			_tempFolder.Delete();
		}

		public static LiftDataMapper CreateDataMapper(string filePath)
		{
			return new LiftDataMapper(
				filePath, null, new ProgressState(),
				new LiftReaderWriterProvider(
					new ProgressState(), new OptionsList(), new string[] { }
				)
			);
		}

		/* NOMORELOCKING
		 [Test]
		[ExpectedException(typeof(IOException))]
		public void Constructor_FileIsWriteableAfterRepositoryIsCreated_Throws()
		{
			using (File.OpenWrite(_persistedFilePath))
			{
			}
		}
		*/

		[Test]
		[ExpectedException(typeof(IOException))]
		public void Constructor_FileIsNotWriteableWhenRepositoryIsCreated_Throws()
		{
			using (File.OpenWrite(_persistedFilePath))
			{
				using(LiftDataMapper dataMapper = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath))
				{
				}
			}
		}

		[Test]
		public void Constructor_FileDoesNotExist_EmptyLiftFileIsCreated()
		{
			string nonExistentFileToBeCreated = Path.GetTempPath() + Path.GetRandomFileName();
			using (LiftRepositoryStateUnitializedTests.CreateDataMapper(nonExistentFileToBeCreated))
			{
			}
			XmlDocument dom = new XmlDocument();
			dom.Load(nonExistentFileToBeCreated);
			Assert.AreEqual(2, dom.ChildNodes.Count);
			Assert.AreEqual("lift", dom.ChildNodes[1].Name);
			Assert.AreEqual(0, dom.ChildNodes[1].ChildNodes.Count);
		}

		[Test]
		public void Constructor_FileIsEmpty_MakeFileAnEmptyLiftFile()
		{
			string emptyFileToBeFilled = Path.GetTempFileName();
			using (LiftRepositoryStateUnitializedTests.CreateDataMapper(emptyFileToBeFilled))
			{
			}
			XmlDocument doc = new XmlDocument();
			doc.Load(emptyFileToBeFilled);
			XmlNode root = doc.DocumentElement;
			Assert.AreEqual("lift", root.Name);
		}

		/* NOMORELOCKING
		  [Test]
		   [ExpectedException(typeof(IOException))]
		   public void LiftIsLocked_ReturnsTrue()
		   {
			   Assert.IsTrue(((LiftDataMapper) DataMapperUnderTest).IsLiftFileLocked);
			   FileStream streamForPermissionChecking = null;
			   try
			   {
				   streamForPermissionChecking = new FileStream(_persistedFilePath, FileMode.Open, FileAccess.Write);
			   }
			   finally
			   {
				   //This is in case the exception is not thrown
				   if (streamForPermissionChecking != null)
				   {
					   streamForPermissionChecking.Close();
					   streamForPermissionChecking.Dispose();
				   }

			   }
		   }
		*/
		[Test]
		public void UnlockedLiftFile_ConstructorDoesNotThrow()
		{
			string persistedFilePath = _tempFolder.GetTemporaryFile();
			persistedFilePath = Path.GetFullPath(persistedFilePath);

			// Confirm that the file is writable.
			FileStream fileStream = File.OpenWrite(persistedFilePath);
			Assert.IsTrue(fileStream.CanWrite);

			// Close it before creating the LiftDataMapper.
			fileStream.Close();

			// LiftDataMapper constructor shouldn't throw an IOException.
			using (LiftDataMapper liftDataMapper = LiftRepositoryStateUnitializedTests.CreateDataMapper(persistedFilePath))
			{
			}
			Assert.IsTrue(true); // Constructor didn't throw.
			File.Delete(persistedFilePath);
		}
	}

	[TestFixture]
	public class LiftRepositoryCreatedFromPersistedData:
		IRepositoryPopulateFromPersistedTests<LexEntry>
	{
		private string _persistedFilePath;
		private TemporaryFolder _tempFolder;

		[SetUp]
		public override void SetUp()
		{
			_tempFolder = new TemporaryFolder();
			_persistedFilePath = _tempFolder.GetTemporaryFile();
			LiftFileInitializer.MakeFile(_persistedFilePath);
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		[TearDown]
		public override void TearDown()
		{
			DataMapperUnderTest.Dispose();
			_tempFolder.Delete();
		}

		protected override void  LastModified_IsSetToMostRecentItemInPersistedDatasLastModifiedTime_v()
		{
			Assert.AreEqual(Item.ModificationTime, DataMapperUnderTest.LastModified);
		}

		protected override void CreateNewRepositoryFromPersistedData()
		{
			DataMapperUnderTest.Dispose();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		/* NOMORELOCKING
	 [Test]
		   [ExpectedException(typeof(IOException))]
		   public void LiftIsLocked_ReturnsTrue()
		   {
			   Assert.IsTrue(((LiftDataMapper) DataMapperUnderTest).IsLiftFileLocked);
			   FileStream streamForPermissionChecking = null;
			   try
			   {
				   streamForPermissionChecking = new FileStream(_persistedFilePath, FileMode.Open, FileAccess.Write);
			   }
			   finally
			   {
				   //This is in case the exception is not thrown
				   if (streamForPermissionChecking != null)
				   {
					   streamForPermissionChecking.Close();
					   streamForPermissionChecking.Dispose();
				   }

			   }
		   }
	 */
	}

	[TestFixture]
	public class LiftRepositoryCreateItemTransitionTests:
		IRepositoryCreateItemTransitionTests<LexEntry>
	{
		private string _persistedFilePath;
		private TemporaryFolder _tempFolder;

		public LiftRepositoryCreateItemTransitionTests()
		{
			_hasPersistOnCreate = false;
		}

		[SetUp]
		public override void SetUp()
		{
			_tempFolder = new TemporaryFolder();
			_persistedFilePath = _tempFolder.GetTemporaryFile();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		[TearDown]
		public override void TearDown()
		{
			DataMapperUnderTest.Dispose();
			_tempFolder.Delete();
		}

		protected override void CreateNewRepositoryFromPersistedData()
		{
			DataMapperUnderTest.Dispose();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		/* NOMORELOCKING
		 [Test]
		 [ExpectedException(typeof(IOException))]
		 public void LiftIsLocked_ReturnsTrue()
		 {
			 SetState();
			 Assert.IsTrue(((LiftDataMapper) DataMapperUnderTest).IsLiftFileLocked);
			 FileStream streamForPermissionChecking = null;
			 try
			 {
				 streamForPermissionChecking = new FileStream(_persistedFilePath, FileMode.Open, FileAccess.Write);
			 }
			 finally
			 {
				 //This is in case the exception is not thrown
				 if (streamForPermissionChecking != null)
				 {
					 streamForPermissionChecking.Close();
					 streamForPermissionChecking.Dispose();
				 }

			 }
		 }
		 */
	}

	[TestFixture]
	public class LiftRepositoryDeleteItemTransitionTests:
		IRepositoryDeleteItemTransitionTests<LexEntry>
	{
		private string _persistedFilePath;
		private TemporaryFolder _tempFolder;

		[SetUp]
		public override void SetUp()
		{
			_tempFolder = new TemporaryFolder();
			_persistedFilePath = _tempFolder.GetTemporaryFile();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		[TearDown]
		public override void TearDown()
		{
			DataMapperUnderTest.Dispose();
			_tempFolder.Delete();
		}

		protected override void CreateNewRepositoryFromPersistedData()
		{
			DataMapperUnderTest.Dispose();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		/* NOMORELOCKING
		 [Test]
			  [ExpectedException(typeof(IOException))]
			  public void LiftIsLocked_ReturnsTrue()
			  {
				  SetState();
				  Assert.IsTrue(((LiftDataMapper) DataMapperUnderTest).IsLiftFileLocked);
				  FileStream streamForPermissionChecking = null;
				  try
				  {
					  streamForPermissionChecking = new FileStream(_persistedFilePath, FileMode.Open, FileAccess.Write);
				  }
				  finally
				  {
					  //This is in case the exception is not thrown
					  if (streamForPermissionChecking != null)
					  {
						  streamForPermissionChecking.Close();
						  streamForPermissionChecking.Dispose();
					  }

				  }
			  }
		 */
	}

	[TestFixture]
	public class LiftRepositoryDeleteIdTransitionTests: IRepositoryDeleteIdTransitionTests<LexEntry>
	{
		private string _persistedFilePath;
		private TemporaryFolder _tempFolder;

		[SetUp]
		public override void SetUp()
		{
			_tempFolder = new TemporaryFolder();
			_persistedFilePath = _tempFolder.GetTemporaryFile();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		[TearDown]
		public override void TearDown()
		{
			DataMapperUnderTest.Dispose();
			_tempFolder.Delete();
		}

		protected override void CreateNewRepositoryFromPersistedData()
		{
			DataMapperUnderTest.Dispose();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		/* NOMORELOCKING
			   [Test]
				[ExpectedException(typeof(IOException))]
				public void LiftIsLocked_ReturnsTrue()
				{
					SetState();
					Assert.IsTrue(((LiftDataMapper) DataMapperUnderTest).IsLiftFileLocked);
					FileStream streamForPermissionChecking = null;
					try
					{
						streamForPermissionChecking = new FileStream(_persistedFilePath, FileMode.Open, FileAccess.Write);
					}
					finally
					{
						//This is in case the exception is not thrown
						if (streamForPermissionChecking != null)
						{
							streamForPermissionChecking.Close();
							streamForPermissionChecking.Dispose();
						}

					}
				}
			   */
	}

	[TestFixture]
	public class LiftRepositoryDeleteAllItemsTransitionTests:
		IRepositoryDeleteAllItemsTransitionTests<LexEntry>
	{
		private string _persistedFilePath;
		private TemporaryFolder _tempFolder;

		[SetUp]
		public override void SetUp()
		{
			_tempFolder = new TemporaryFolder();
			_persistedFilePath = _tempFolder.GetTemporaryFile();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		[TearDown]
		public override void TearDown()
		{
			DataMapperUnderTest.Dispose();
			_tempFolder.Delete();
		}

		protected override void RepopulateRepositoryFromPersistedData()
		{
			DataMapperUnderTest.Dispose();
			DataMapperUnderTest = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}

		/* NOMORELOCKING
				 [Test]
				[ExpectedException(typeof(IOException))]
				public void LiftIsLocked_ReturnsTrue()
				{
					SetState();
					Assert.IsTrue(((LiftDataMapper) DataMapperUnderTest).IsLiftFileLocked);
					FileStream streamForPermissionChecking = null;
					try
					{
						streamForPermissionChecking = new FileStream(_persistedFilePath, FileMode.Open, FileAccess.Write);
					}
					finally
					{
						//This is in case the exception is not thrown
						if(streamForPermissionChecking != null)
						{
							streamForPermissionChecking.Close();
							streamForPermissionChecking.Dispose();
						}

					}
				}
				 */
	}

	[TestFixture]
	public class LiftFileAlreadyLockedTest
	{
		private string _persistedFilePath;
		private FileStream _fileStream;

		[SetUp]
		public void SetUp()
		{
			_persistedFilePath = Path.GetRandomFileName();
			_persistedFilePath = Path.GetFullPath(_persistedFilePath);
			_fileStream = File.OpenWrite(_persistedFilePath);
		}

		[TearDown]
		public void TearDown()
		{
			_fileStream.Close();
			File.Delete(_persistedFilePath);
		}

		[Test]
		[ExpectedException(typeof(IOException))]
		public void LockedFile_Throws()
		{
			Assert.IsTrue(_fileStream.CanWrite);
			LiftDataMapper liftDataMapper = LiftRepositoryStateUnitializedTests.CreateDataMapper(_persistedFilePath);
		}
	}
}
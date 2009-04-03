﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace Palaso.Backup
{
	//In order for these tests to be relevant you must attach 2 usb drives to your computer and adjust
	//their expected size and path in the TestFixtureSetup() method
	[TestFixture]
	//[Ignore("Hardware specific")]
	public class UsbDeviceInfoTests
	{
		private struct driveParamsForTests
		{
			public ulong driveSize;
			public DirectoryInfo path;
		}

		private driveParamsForTests drive0;
		private driveParamsForTests drive1;

		[TestFixtureSetUp]
		public void TestFixtureSetup()
		{
#if MONO
			drive0.driveSize = 256850432;
			drive0.path = new DirectoryInfo("/media/Kingston");
			drive1.driveSize = 1032724480;
			drive1.path = new DirectoryInfo("/media/PAXERIT");
#else
			drive0.driveSize = 256770048;
			drive0.path = new DirectoryInfo("E:\\");
			drive1.driveSize = 1032454144;
			drive1.path = new DirectoryInfo("J:\\");
#endif
		}

		[Test]
		public void GetDrives_2DrivesArePluggedIn_DrivesAreReturned()
		{
			List<UsbDriveInfo> usbDrives = UsbDriveInfo.GetDrives();
			Assert.AreEqual(2, usbDrives.Count);
		}

		[Test]
		public void TotalSize_2DrivesArePluggedIn_TheDrivesSizesAreCorrect()
		{
			List<UsbDriveInfo> usbDrives = UsbDriveInfo.GetDrives();
			Assert.AreEqual(drive0.driveSize, usbDrives[0].TotalSize);
			Assert.AreEqual(drive1.driveSize, usbDrives[1].TotalSize);
		}

		[Test]
		public void RootDirectory_2DrivesArePluggedInAndReady_TheDrivesPathsCorrect()
		{
			List<UsbDriveInfo> usbDrives = UsbDriveInfo.GetDrives();
			Assert.AreEqual(drive0.path.FullName, usbDrives[0].RootDirectory.FullName);
			Assert.AreEqual(drive1.path.FullName, usbDrives[1].RootDirectory.FullName);
		}

		[Test]
		public void IsReady_2DrivesAreMounted_ReturnsTrue()
		{
			List<UsbDriveInfo> usbDrives = UsbDriveInfo.GetDrives();
			Assert.IsTrue(usbDrives[0].IsReady);
			Assert.IsTrue(usbDrives[1].IsReady);
		}

		[Test]
		public void IsReady_2DrivesAreNotMounted_ReturnsFalse()
		{
			List<UsbDriveInfo> usbDrives = UsbDriveInfo.GetDrives();
			Assert.IsFalse(usbDrives[0].IsReady);
			Assert.IsFalse(usbDrives[1].IsReady);
		}

		[Test]
		[ExpectedException(typeof(ArgumentException))]
		public void RootDirectory_2DrivesAreNotMounted_Throws()
		{
			List<UsbDriveInfo> usbDrives = UsbDriveInfo.GetDrives();
			Assert.AreEqual(drive0.path.FullName, usbDrives[0].RootDirectory.FullName);
			Console.WriteLine(usbDrives[0].RootDirectory.FullName);
		}
	}
}
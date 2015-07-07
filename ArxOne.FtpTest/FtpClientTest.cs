﻿#region Arx One FTP
// Arx One FTP
// A simple FTP client
// https://github.com/ArxOne/FTP
// Released under MIT license http://opensource.org/licenses/MIT
#endregion
namespace ArxOne.FtpTest
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Web;
    using Ftp;
    using Ftp.Platform;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    ///This is a test class for FtpClientTest and is intended
    ///to contain all FtpClientTest Unit Tests
    ///</summary>
    [TestClass]
    public partial class FtpClientTest
    {
        [DebuggerDisplay("{HostType}-->{Uri}")]
        private class TestHost
        {
            public string HostType { get; set; }
            public Uri Uri { get; set; }
            public NetworkCredential Credential { get; set; }
        }

        // Credentials.txt is a simple text file with URI (including credentials) formed as follows:
        // - simple uri (such as 'ftp://user:pass@host:21')
        // - specific host type (such as 'win-->ftp://user:pass@host:21').
        // Please also note that first match is returned, so if only a protocol is asked, then any host type may be returned

        private static IEnumerable<TestHost> EnumerateCredentials()
        {
            const string credentialsTxt = "credentials.txt";
            if (!File.Exists(credentialsTxt))
                Assert.Inconclusive("File '{0}' not found", credentialsTxt);
            using (var streamReader = File.OpenText(credentialsTxt))
            {
                for (; ; )
                {
                    var line = streamReader.ReadLine();
                    if (line == null)
                        yield break;

                    var typeAndUri = line.Split(new[] { "-->" }, 2, StringSplitOptions.RemoveEmptyEntries);
                    string hostType;
                    string uriAndCredentials;

                    if (typeAndUri.Length == 1)
                    {
                        hostType = null;
                        uriAndCredentials = line;
                    }
                    else
                    {
                        hostType = typeAndUri[0];
                        uriAndCredentials = typeAndUri[1];
                    }

                    Uri uri;
                    try
                    {
                        uri = new Uri(uriAndCredentials);
                    }
                    catch (UriFormatException)
                    {
                        continue;
                    }
                    var literalLoginAndPassword = HttpUtility.UrlDecode(uri.UserInfo.Replace("_at_", "@"));
                    var loginAndPassword = literalLoginAndPassword.Split(new[] { ':' }, 2);
                    var networkCredential = loginAndPassword.Length == 2
                        ? new NetworkCredential(loginAndPassword[0], loginAndPassword[1])
                        : CredentialCache.DefaultNetworkCredentials;
                    yield return new TestHost { HostType = hostType, Uri = uri, Credential = networkCredential };
                }
            }
        }

        private static TestHost GetTestHost(string protocol, string hostType = null)
        {
            var t = EnumerateCredentials().FirstOrDefault(c => c.Uri.Scheme == protocol && (hostType == null || c.HostType == hostType));
            if (t == null)
            {
                if (hostType == null)
                    Assert.Inconclusive("Found no configuration for protocol '{0}'", protocol);
                else
                    Assert.Inconclusive("Found no configuration for protocol '{0}' and host type '{1}'", protocol, hostType);
            }
            return t;
        }

        /// <summary>
        ///A test for ParseUnix
        ///</summary>
        [TestMethod]
        [TestCategory("FtpClient")]
        public void ParseUnix1Test()
        {
            var entry = FtpPlatform.ParseUnix("drwxr-xr-x    4 1001     1001         4096 Jan 21 14:41 nas-1", null);
            Assert.IsNotNull(entry);
            Assert.AreEqual(FtpEntryType.Directory, entry.Type);
            Assert.AreEqual("nas-1", entry.Name);
        }

        /// <summary>
        ///A test for ParseUnix
        ///</summary>
        [TestMethod]
        [TestCategory("FtpClient")]
        public void ParseUnix2Test()
        {
            var entry = FtpPlatform.ParseUnix("drwxr-xr-x    4 nas-1    nas-1        4096 Jan 21 15:41 nas-1", null);
            Assert.IsNotNull(entry);
            Assert.AreEqual(FtpEntryType.Directory, entry.Type);
            Assert.AreEqual("nas-1", entry.Name);
        }

        /// <summary>
        ///A test for ParseUnix
        ///</summary>
        [TestMethod]
        [TestCategory("FtpClient")]
        public void ParseUnix3Test()
        {
            var entry = FtpPlatform.ParseUnix("lrwxrwxrwx    1 0        0               4 Sep 03  2009 lib64 -> /lib", null);
            Assert.IsNotNull(entry);
            Assert.AreEqual(FtpEntryType.Link, entry.Type);
            Assert.AreEqual("lib64", entry.Name);
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Windows")]
        public void ParseWindowsTest()
        {
            var entry = WindowsFtpPlatform.ParseLine("    03-07-15  03:52PM                22286 03265480-photo-logo.png", null);
            Assert.IsNotNull(entry);
            Assert.AreEqual(FtpEntryType.File, entry.Type);
            Assert.AreEqual("03265480-photo-logo.png", entry.Name);
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Windows")]
        public void ParseWindows2Test()
        {
            var entry = WindowsFtpPlatform.ParseLine("    04-04-15  12:12PM       <DIR>          New folder", null);
            Assert.IsNotNull(entry);
            Assert.AreEqual(FtpEntryType.Directory, entry.Type);
            Assert.AreEqual("New folder", entry.Name);
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void FtpListTest()
        {
            FtpListTest(true);
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void FtpActiveListTest()
        {
            FtpListTest(false);
        }

        private static void FtpListTest(bool passive, string hostType = null)
        {
            var ftpTestHost = GetTestHost("ftp", hostType);
            using (var ftpClient = new FtpClient(ftpTestHost.Uri, ftpTestHost.Credential, new FtpClientParameters { Passive = passive }))
            {
                var list = ftpClient.ListEntries("/");
                if (ftpClient.ServerType == FtpServerType.Unix)
                    Assert.IsTrue(list.Any(e => e.Name == "tmp"));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void FtpStatTest()
        {
            var ftpTestHost = GetTestHost("ftp");
            using (var ftpClient = new FtpClient(ftpTestHost.Uri, ftpTestHost.Credential))
            {
                var list = ftpClient.StatEntries("/");
                Assert.IsTrue(list.Any(e => e.Name == "tmp"));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void FtpStatNoDotTest()
        {
            var ftpTestHost = GetTestHost("ftp");
            using (var ftpClient = new FtpClient(ftpTestHost.Uri, ftpTestHost.Credential))
            {
                var list = ftpClient.StatEntries("/");
                Assert.IsFalse(list.Any(e => e.Name == "." || e.Name == ".."));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void FtpesStatTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                var list = ftpClient.StatEntries("/").ToArray();
                Assert.IsTrue(list.Any(e => e.Name == "tmp"));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void ReadFileTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                using (var s = ftpClient.Retr("/var/log/installer/status"))
                using (var t = new StreamReader(s, Encoding.UTF8))
                {
                    var m = t.ReadToEnd();
                    Assert.IsTrue(m.Length > 0);
                }
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void CreateFileTest()
        {
            CreateFileTest(true);
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void ActiveCreateFileTest()
        {
            CreateFileTest(false);
        }
        public void CreateFileTest(bool passive, string hostType = null)
        {
            var ftpesTestHost = GetTestHost("ftp", hostType);
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential, new FtpClientParameters { Passive = passive }))
            {
                var directory = ftpClient.ServerType == FtpServerType.Windows ? "/" : "/tmp/";
                var path = directory + "file." + Guid.NewGuid();
                using (var s = ftpClient.Stor(path))
                {
                    s.WriteByte(65);
                }
                using (var r = ftpClient.Retr(path))
                {
                    Assert.IsNotNull(r);
                    Assert.AreEqual(65, r.ReadByte());
                    Assert.AreEqual(-1, r.ReadByte());
                }
                ftpClient.Dele(path);
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void DeleteFileTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                var path = "/tmp/file." + Guid.NewGuid();
                using (var s = ftpClient.Stor(path))
                {
                    s.WriteByte(65);
                }
                Assert.IsTrue(ftpClient.Dele(path));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void DeleteFolderTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                var path = "/tmp/file." + Guid.NewGuid();
                ftpClient.Mkd(path);
                Assert.IsTrue(ftpClient.Rmd(path));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void DeleteTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                var path = "/tmp/file." + Guid.NewGuid();
                ftpClient.Mkd(path);
                Assert.IsTrue(ftpClient.Delete(path));
                using (var s = ftpClient.Stor(path))
                {
                    s.WriteByte(65);
                }
                Assert.IsTrue(ftpClient.Delete(path));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void CreateSpecialNameFolderTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                var path = "/tmp/file." + Guid.NewGuid() + "(D)";
                ftpClient.Mkd(path);
                Assert.IsTrue(ftpClient.Rmd(path));
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void CreateNonExistingSubFolderTest()
        {
            var ftpesTestHost = GetTestHost("ftpes");
            using (var ftpClient = new FtpClient(ftpesTestHost.Uri, ftpesTestHost.Credential))
            {
                var parent = "/tmp/" + Guid.NewGuid().ToString("N");
                var child = parent + "/" + Guid.NewGuid().ToString("N");
                var reply = ftpClient.SendSingleCommand("MKD", child);
                Assert.AreEqual(550, reply.Code.Code);
                ftpClient.SendSingleCommand("RMD", parent);
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void CreateFolderTwiceTest()
        {
            var testHost = GetTestHost("ftpes");
            var client = new FtpClient(testHost.Uri, testHost.Credential);
            using (var ftpClient = client)
            {
                var path = "/tmp/" + Guid.NewGuid().ToString("N");
                var reply = ftpClient.SendSingleCommand("MKD", path);
                var reply2 = ftpClient.SendSingleCommand("MKD", path);
                Assert.AreEqual(550, reply2.Code.Code);
                ftpClient.Rmd(path);
            }
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void FileExistsTest()
        {
            var ftpTestHost = GetTestHost("ftp");
            using (var ftpClient = new FtpClient(ftpTestHost.Uri, ftpTestHost.Credential))
            {
                const string directory = "/lib/";
                var list = ftpClient.StatEntries(directory).ToList();
                var oneFile = list.First(e => e.Type == FtpEntryType.File);
                var oneDirectory = list.First(e => e.Type == FtpEntryType.Directory);
                var oneLink = list.First(e => e.Type == FtpEntryType.Link);

                var oneFileEntry = ftpClient.GetEntry(directory + oneFile.Name);
                Assert.IsNotNull(oneFileEntry);
                Assert.AreEqual(FtpEntryType.File, oneFileEntry.Type);
                var oneDirectoryEntry = ftpClient.GetEntry(directory + oneDirectory.Name);
                Assert.IsNotNull(oneDirectoryEntry);
                Assert.AreEqual(FtpEntryType.Directory, oneDirectoryEntry.Type);
                var oneLinkEntry = ftpClient.GetEntry(directory + oneLink.Name);
                Assert.IsNotNull(oneLinkEntry);
                Assert.AreEqual(FtpEntryType.Link, oneLinkEntry.Type);
            }
        }
        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void SpaceNameTest()
        {
            FolderAndChildTest(GetTestHost("ftp"), "A and B", "C and D");
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void BracketsNameTest()
        {
            FolderAndChildTest(GetTestHost("ftp"), "X[]Y", "Z{}[]T");
        }

        [TestMethod]
        [TestCategory("FtpClient")]
        [TestCategory("Credentials")]
        public void ParenthesisNameTest()
        {
            FolderAndChildTest(GetTestHost("ftp"), "i()j", "k()l");
        }

        private void FolderAndChildTest(TestHost testHost, string folderName, string childName)
        {
            using (var ftpClient = new FtpClient(testHost.Uri, testHost.Credential))
            {
                var folder = (ftpClient.ServerType == FtpServerType.Windows ? "/" : "/tmp/") + folderName;
                var file = folder + "/" + childName;
                try
                {
                    ftpClient.Mkd(folder);
                    using (var s = ftpClient.Stor(file))
                        s.WriteByte(123);

                    var c = ftpClient.ListEntries(folder).SingleOrDefault();
                    Assert.IsNotNull(c);
                    Assert.AreEqual(childName, c.Name);
                    var c2 = ftpClient.StatEntries(folder).SingleOrDefault();
                    Assert.IsNotNull(c2);
                    Assert.AreEqual(childName, c2.Name);

                    using (var r = ftpClient.Retr(file))
                    {
                        Assert.AreEqual(123, r.ReadByte());
                        Assert.AreEqual(-1, r.ReadByte());
                    }
                }
                finally
                {
                    ftpClient.Dele(file);
                    ftpClient.Rmd(folder);
                }
            }
        }
    }
}

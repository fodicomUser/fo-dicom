// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace Dicom.Media
{
    using System.IO;
    using System.Threading.Tasks;

    using Xunit;

    [Collection("General")]
    public class DicomDirectoryTest
    {
        #region Unit tests

        private const string SrDocDicomDirFilePath = @".\Test Data\SrDocDicomDir";

        [Fact]
        public void Open_DicomDirFile_Succeeds()
        {
            var dir = DicomDirectory.Open(@".\Test Data\DICOMDIR");

            var expected = DicomUID.MediaStorageDirectoryStorage.UID;
            var actual = dir.FileMetaInfo.MediaStorageSOPClassUID.UID;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task OpenAsync_DicomDirFile_Succeeds()
        {
            var dir = await DicomDirectory.OpenAsync(@".\Test Data\DICOMDIR");

            var expected = DicomUID.MediaStorageDirectoryStorage.UID;
            var actual = dir.FileMetaInfo.MediaStorageSOPClassUID.UID;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Open_MediaStorageSOPInstanceUID_ShouldBeConsistent()
        {
            var dir = DicomDirectory.Open(@".\Test Data\DICOMDIR");
            var expected = dir.FileMetaInfo.Get<DicomUID>(DicomTag.MediaStorageSOPInstanceUID).UID;
            var actual = dir.MediaStorageSOPInstanceUID.UID;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Open_DicomDirStream_Succeeds()
        {
            using (var stream = File.OpenRead(@".\Test Data\DICOMDIR"))
            {
                DicomDirectory dir = DicomDirectory.Open(stream);

                var expected = DicomUID.MediaStorageDirectoryStorage.UID;
                var actual = dir.FileMetaInfo.MediaStorageSOPClassUID.UID;
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public async Task OpenAsync_DicomDirStream_Succeeds()
        {
            using (var stream = File.OpenRead(@".\Test Data\DICOMDIR"))
            {
                DicomDirectory dir = await DicomDirectory.OpenAsync(stream);

                var expected = DicomUID.MediaStorageDirectoryStorage.UID;
                var actual = dir.FileMetaInfo.MediaStorageSOPClassUID.UID;
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void AddFile_AnonymizedSeries_AllFilesAddedToSameStudySeriesNode()
        {
            var dicomFiles = GetDicomFilesFromWebZip(
                "https://www.creatis.insa-lyon.fr/~jpr/PUBLIC/gdcm/gdcmSampleData/Philips_Medical_Images/mr711-mr712/abd1.zip");

            // Anonymize all files
            var anonymizer = new DicomAnonymizer();
            foreach (var dicomFile in dicomFiles)
            {
                anonymizer.AnonymizeInPlace(dicomFile);
            }

            // Create DICOM directory
            var dicomDir = new DicomDirectory();
            foreach (var dicomFile in dicomFiles)
            {
                dicomDir.AddFile(dicomFile, RecordTypeName.Image);
            }

            var imageNodes = dicomDir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                .LowerLevelDirectoryRecordCollection;
            Assert.Equal(dicomFiles.Count, imageNodes.Count());
        }

        private static IList<DicomFile> GetDicomFilesFromWebZip(string url)
        {
            var dicomFiles = new List<DicomFile>();

            using (var webClient = new WebClient())
            {
                var bytes = webClient.DownloadData(url);

                using (var stream = new MemoryStream(bytes))
                using (var zipper = new ZipArchive(stream))
                {
                    foreach (var entry in zipper.Entries)
                    {
                        try
                        {
                            using (var entryStream = entry.Open())
                            using (var duplicate = new MemoryStream())
                            {
                                entryStream.CopyTo(duplicate);
                                duplicate.Seek(0, SeekOrigin.Begin);
                                var dicomFile = DicomFile.Open(duplicate);
                                dicomFiles.Add(dicomFile);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return dicomFiles;
        }

        [Fact]
        public void AddFile_SrDoc_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var srDocFileCount = Directory.GetFiles(@".\Test Data", "SrDoc*").Length;
            var srDocDicomFiles = GetSrDocDicomFiles();
            var dicomdir = new DicomDirectory();

            foreach (var dicomFile in srDocDicomFiles.Values)
            {
                dicomdir.AddFile(dicomFile, RecordTypeName.SrDocument);
            }

            var srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                .LowerLevelDirectoryRecordCollection.Count();
            Assert.Equal(srDocFileCount, srDocRecordCount);

            dicomdir.Save(SrDocDicomDirFilePath);
            dicomdir = DicomDirectory.Open(SrDocDicomDirFilePath);

            srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                .LowerLevelDirectoryRecordCollection.Count();
            Assert.Equal(srDocFileCount, srDocRecordCount);
        }

        [Fact]
        public void Find_SrDoc_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var srDocDicomFiles = GetSrDocDicomFiles();
            var dicomdir = new DicomDirectory();

            foreach (var dicomFile in srDocDicomFiles.Values)
            {
                dicomdir.AddFile(dicomFile, RecordTypeName.SrDocument);
            }

            var seriesRecord = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord;

            foreach (var sopInstanceUid in srDocDicomFiles.Keys)
            {
                var srDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, sopInstanceUid);
                Assert.NotEqual(null, srDocRecord);
            }
        }

        [Fact]
        public void Remove_FirstSrDocRecord_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var srDocFiles = GetSrDocDicomFiles();
            DicomDirectory dicomdir = new DicomDirectory();

            foreach (var dicomFile in srDocFiles.Values)
            {
                dicomdir.AddFile(dicomFile, RecordTypeName.SrDocument);
            }

            var firstSrDocRecordSopInstanceUid = srDocFiles.Keys.First();
            var seriesRecord = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord;
            DicomDirectoryRecord previousRecord = null;
            DicomDirectoryRecord firstSrDocRecord = null;

            dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, firstSrDocRecordSopInstanceUid,
                out firstSrDocRecord, out previousRecord);

            dicomdir.Remove(firstSrDocRecord, previousRecord, seriesRecord);

            var srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                  .LowerLevelDirectoryRecordCollection.Count();
            firstSrDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, firstSrDocRecordSopInstanceUid);
            
            Assert.Equal(null, firstSrDocRecord);
            Assert.Equal(4, srDocRecordCount);

            dicomdir.Save(SrDocDicomDirFilePath);
            dicomdir = DicomDirectory.Open(SrDocDicomDirFilePath);

            srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                   .LowerLevelDirectoryRecordCollection.Count();
            firstSrDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, firstSrDocRecordSopInstanceUid);

            Assert.Equal(null, firstSrDocRecord);
            Assert.Equal(4, srDocRecordCount);
        }

        [Fact]
        public void Remove_OneSrDocRecord_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var srDocFiles = GetSrDocDicomFiles();
            DicomDirectory dicomdir = new DicomDirectory();

            foreach (var dicomFile in srDocFiles.Values)
            {
                dicomdir.AddFile(dicomFile, RecordTypeName.SrDocument);
            }

            var oneSrDocRecordSopInstanceUid = srDocFiles.Keys.ElementAt(2);
            var seriesRecord = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord;
            DicomDirectoryRecord previousRecord = null;
            DicomDirectoryRecord oneSrDocRecord = null;
            dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, oneSrDocRecordSopInstanceUid,
                out oneSrDocRecord, out previousRecord);

            dicomdir.Remove(oneSrDocRecord, previousRecord, seriesRecord);
            var srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                   .LowerLevelDirectoryRecordCollection.Count();
            oneSrDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, oneSrDocRecordSopInstanceUid);

            Assert.Equal(null, oneSrDocRecord);
            Assert.Equal(4, srDocRecordCount);

            dicomdir.Save(SrDocDicomDirFilePath);
            dicomdir = DicomDirectory.Open(SrDocDicomDirFilePath);

            srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                   .LowerLevelDirectoryRecordCollection.Count();
            oneSrDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, oneSrDocRecordSopInstanceUid);

            Assert.Equal(null, oneSrDocRecord);
            Assert.Equal(4, srDocRecordCount);
        }

        [Fact]
        public void Remove_LastSrDocRecord_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var srDocFiles = GetSrDocDicomFiles();
            DicomDirectory dicomdir = new DicomDirectory();

            foreach (var dicomFile in srDocFiles.Values)
            {
                dicomdir.AddFile(dicomFile, RecordTypeName.SrDocument);
            }

            var lastSrDocRecordSopInstanceUid = srDocFiles.Keys.Last();
            var seriesRecord = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord;

            DicomDirectoryRecord previousRecord = null;
            DicomDirectoryRecord lastSrDocRecord = null;
            dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, lastSrDocRecordSopInstanceUid,
                out lastSrDocRecord, out previousRecord);

            dicomdir.Remove(lastSrDocRecord, previousRecord, seriesRecord);
            var srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                   .LowerLevelDirectoryRecordCollection.Count();
            lastSrDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, lastSrDocRecordSopInstanceUid);

            Assert.Equal(null, lastSrDocRecord);
            Assert.Equal(4, srDocRecordCount);

            dicomdir.Save(SrDocDicomDirFilePath);
            dicomdir = DicomDirectory.Open(SrDocDicomDirFilePath);

            srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                   .LowerLevelDirectoryRecordCollection.Count();
            lastSrDocRecord = dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, lastSrDocRecordSopInstanceUid);

            Assert.Equal(null, lastSrDocRecord);
            Assert.Equal(4, srDocRecordCount);
        }

        [Fact]
        public void Remove_AllSrDocRecords_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var srDocFiles = GetSrDocDicomFiles();
            DicomDirectory dicomdir = new DicomDirectory();

            foreach (var dicomFile in srDocFiles.Values)
            {
                dicomdir.AddFile(dicomFile, RecordTypeName.SrDocument);
            }

            var seriesRecord = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord;

            foreach (var oneSrDocRecordSopInstanceUid in srDocFiles.Keys)
            {
                DicomDirectoryRecord previousRecord = null;
                DicomDirectoryRecord oneSrDocRecord = null;
                dicomdir.Find(DicomTag.ReferencedSOPInstanceUIDInFile, seriesRecord, oneSrDocRecordSopInstanceUid,
                    out oneSrDocRecord, out previousRecord);

                dicomdir.Remove(oneSrDocRecord, previousRecord, seriesRecord);
            }

            var srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                .LowerLevelDirectoryRecordCollection.Count();
            Assert.Equal(0, srDocRecordCount);

            dicomdir.Save(SrDocDicomDirFilePath);
            dicomdir = DicomDirectory.Open(SrDocDicomDirFilePath);

            srDocRecordCount = dicomdir.RootDirectoryRecord.LowerLevelDirectoryRecord.LowerLevelDirectoryRecord
                .LowerLevelDirectoryRecordCollection.Count();
            Assert.Equal(0, srDocRecordCount);
        }

        [Fact]
        public void Remove_UniquePatient_Succeeds()
        {
            ClearSrDocDicomDirFile();

            var patientId = "T000002";
            var srDocFiles = GetSrDocDicomFiles();
            DicomDirectory dicomdir = new DicomDirectory();

            dicomdir.AddFile(srDocFiles.Values.First(), RecordTypeName.SrDocument);

            DicomDirectoryRecord previousRecord = null;
            DicomDirectoryRecord patientRecord = null;
            dicomdir.Find(DicomTag.PatientID, dicomdir, patientId, out patientRecord, out previousRecord);
            dicomdir.Remove(patientRecord, previousRecord, dicomdir);

            var patientRecords = dicomdir.RootDirectoryRecordCollection;
            patientRecord = dicomdir.Find(DicomTag.PatientID, dicomdir, patientId);

            Assert.Equal(null, patientRecord);
            Assert.Equal(null, patientRecords);

            dicomdir.Save(SrDocDicomDirFilePath);
            dicomdir = DicomDirectory.Open(SrDocDicomDirFilePath);

            patientRecords = dicomdir.RootDirectoryRecordCollection;
            patientRecord = dicomdir.Find(DicomTag.PatientID, dicomdir, patientId);

            Assert.Equal(null, patientRecord);
            Assert.Equal(null, patientRecords);
        }

        private static IDictionary<string, DicomFile> GetSrDocDicomFiles()
        {
            var dicomFiles = new Dictionary<string, DicomFile>();

            foreach (var file in Directory.GetFiles(@".\Test Data", "SrDoc*"))
            {
                var dicomFile = DicomFile.Open(file);
                dicomFiles.Add(dicomFile.Dataset.Get<string>(DicomTag.SOPInstanceUID), dicomFile);
            }

            return dicomFiles;
        }

        private static void ClearSrDocDicomDirFile()
        {
            if (File.Exists(SrDocDicomDirFilePath))
            {
                File.Delete(SrDocDicomDirFilePath);
            }
        }

        #endregion
    }
}

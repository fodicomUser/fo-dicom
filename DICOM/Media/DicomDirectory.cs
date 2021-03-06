﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Media
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;

    #if !NET35
    using System.Threading.Tasks;
    #endif

    using Dicom.IO;
    using Dicom.IO.Reader;
    using Dicom.IO.Writer;

    /// <summary>
    /// Class for managing DICOM directory objects.
    /// </summary>
    public class DicomDirectory : DicomFile
    {
        #region Properties and Attributes

        private DicomSequence _directoryRecordSequence;

        private uint _fileOffset;

        /// <summary>
        /// Gets the root directory record.
        /// </summary>
        public DicomDirectoryRecord RootDirectoryRecord { get; private set; }

        /// <summary>
        /// Gets the root directory record collection.
        /// </summary>
        public DicomDirectoryRecordCollection RootDirectoryRecordCollection
            => new DicomDirectoryRecordCollection(this.RootDirectoryRecord);

        /// <summary>
        /// Gets or sets the file set ID.
        /// </summary>
        /// <exception cref="ArgumentException">If applied file set ID is null or empty.</exception>
        public string FileSetID
        {
            get
            {
                return Dataset.Get<string>(DicomTag.FileSetID);
            }
            set
            {
                if (!string.IsNullOrEmpty(value?.Trim()))
                {
                    Dataset.AddOrUpdate(DicomTag.FileSetID, value);
                }
                else
                {
                    throw new ArgumentException("File Set ID must not be null or empty.", nameof(value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the source application entity title.
        /// </summary>
        public string SourceApplicationEntityTitle
        {
            get
            {
                return FileMetaInfo.SourceApplicationEntityTitle;
            }
            set
            {
                FileMetaInfo.SourceApplicationEntityTitle = value;
            }
        }

        /// <summary>
        /// Gets or sets the media storage SOP instance UID.
        /// </summary>
        public DicomUID MediaStorageSOPInstanceUID
        {
            get
            {
                return FileMetaInfo.MediaStorageSOPInstanceUID;
            }
            set
            {
                FileMetaInfo.MediaStorageSOPInstanceUID = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DicomDirectory"/> class.
        /// </summary>
        /// <param name="explicitVr">Indicates whether or not Value Representation of the DICOM directory should be explicit.</param>
        public DicomDirectory(bool explicitVr = true)
        {
            FileMetaInfo.Version = new byte[] { 0x00, 0x01 };
            FileMetaInfo.MediaStorageSOPClassUID = DicomUID.MediaStorageDirectoryStorage;
            FileMetaInfo.MediaStorageSOPInstanceUID = DicomUID.Generate();
            FileMetaInfo.TransferSyntax = explicitVr
                                              ? DicomTransferSyntax.ExplicitVRLittleEndian
                                              : DicomTransferSyntax.ImplicitVRLittleEndian;
            FileMetaInfo.ImplementationClassUID = DicomImplementation.ClassUID;
            FileMetaInfo.ImplementationVersionName = DicomImplementation.Version;

            _directoryRecordSequence = new DicomSequence(DicomTag.DirectoryRecordSequence);

            Dataset.Add<string>(DicomTag.FileSetID, string.Empty)
                .Add<ushort>(DicomTag.FileSetConsistencyFlag, 0)
                .Add<uint>(DicomTag.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity, 0)
                .Add<uint>(DicomTag.OffsetOfTheLastDirectoryRecordOfTheRootDirectoryEntity, 0)
                .Add(_directoryRecordSequence);
        }

        /// <summary>
        /// Creates an instance of the <see cref="DicomDirectory"/> class File Meta Information and DICOM dataset are not initialized.
        /// </summary>
        /// <remarks>Intended to be used e.g. by the static Open methods to construct an empty <see cref="DicomDirectory"/> object subject to filling.</remarks>
        private DicomDirectory()
        {
        }

        #endregion

        #region Save/Load Methods

        /// <summary>
        /// Read DICOM Directory.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <returns><see cref="DicomDirectory"/> instance.</returns>
        public static new DicomDirectory Open(string fileName)
        {
            return Open(fileName, DicomEncoding.Default);
        }

        /// <summary>
        /// Read DICOM Directory.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <param name="fallbackEncoding">Encoding to apply if it cannot be identified from DICOM directory.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns><see cref="DicomDirectory"/> instance.</returns>
        public static new DicomDirectory Open(string fileName, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException(nameof(fallbackEncoding));
            }
            var df = new DicomDirectory();

            try
            {
                df.File = IOManager.CreateFileReference(fileName);

                using (var source = new FileByteSource(df.File))
                {
                    var reader = new DicomFileReader();
                    var dirObserver = new DicomDirectoryReaderObserver(df.Dataset);

                    var result = reader.Read(
                        source,
                        new DicomDatasetReaderObserver(df.FileMetaInfo),
                        new DicomReaderMultiObserver(
                            new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                            dirObserver),
                        stop);

                    return FinalizeDicomDirectoryLoad(df, reader, dirObserver, result);
                }
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

        /// <summary>
        /// Read DICOM Directory.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <returns><see cref="DicomDirectory"/> instance.</returns>
        public static new DicomDirectory Open(Stream stream)
        {
            return Open(stream, DicomEncoding.Default);
        }

        /// <summary>
        /// Read DICOM Directory from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <param name="fallbackEncoding">Encoding to apply if it cannot be identified from DICOM directory.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns><see cref="DicomDirectory"/> instance.</returns>
        public static new DicomDirectory Open(Stream stream, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException(nameof(fallbackEncoding));
            }
            var df = new DicomDirectory();

            try
            {
                var source = new StreamByteSource(stream);

                var reader = new DicomFileReader();
                var dirObserver = new DicomDirectoryReaderObserver(df.Dataset);

                var result = reader.Read(
                    source,
                    new DicomDatasetReaderObserver(df.FileMetaInfo),
                    new DicomReaderMultiObserver(
                        new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                        dirObserver),
                    stop);

                return FinalizeDicomDirectoryLoad(df, reader, dirObserver, result);
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

#if !NET35
        /// <summary>
        /// Asynchronously read DICOM Directory.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <returns>Awaitable <see cref="DicomDirectory"/> instance.</returns>
        public static new Task<DicomDirectory> OpenAsync(string fileName)
        {
            return OpenAsync(fileName, DicomEncoding.Default);
        }

        /// <summary>
        /// Asynchronously read DICOM Directory.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <param name="fallbackEncoding">Encoding to apply if it cannot be identified from DICOM directory.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns>Awaitable <see cref="DicomDirectory"/> instance.</returns>
        public static new async Task<DicomDirectory> OpenAsync(string fileName, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException(nameof(fallbackEncoding));
            }
            var df = new DicomDirectory();

            try
            {
                df.File = IOManager.CreateFileReference(fileName);

                using (var source = new FileByteSource(df.File))
                {
                    var reader = new DicomFileReader();
                    var dirObserver = new DicomDirectoryReaderObserver(df.Dataset);

                    var result =
                        await
                        reader.ReadAsync(
                            source,
                            new DicomDatasetReaderObserver(df.FileMetaInfo),
                            new DicomReaderMultiObserver(
                            new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                            dirObserver),
                            stop).ConfigureAwait(false);

                    return FinalizeDicomDirectoryLoad(df, reader, dirObserver, result);
                }
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

        /// <summary>
        /// Asynchronously read DICOM Directory from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <returns>Awaitable <see cref="DicomDirectory"/> instance.</returns>
        public static new Task<DicomDirectory> OpenAsync(Stream stream)
        {
            return OpenAsync(stream, DicomEncoding.Default);
        }

        /// <summary>
        /// Asynchronously read DICOM Directory from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <param name="fallbackEncoding">Encoding to apply if it cannot be identified from DICOM directory.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns>Awaitable <see cref="DicomDirectory"/> instance.</returns>
        public static new async Task<DicomDirectory> OpenAsync(Stream stream, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException(nameof(fallbackEncoding));
            }
            var df = new DicomDirectory();

            try
            {
                var source = new StreamByteSource(stream);

                var reader = new DicomFileReader();
                var dirObserver = new DicomDirectoryReaderObserver(df.Dataset);

                var result =
                    await
                    reader.ReadAsync(
                        source,
                        new DicomDatasetReaderObserver(df.FileMetaInfo),
                        new DicomReaderMultiObserver(
                        new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                        dirObserver),
                        stop).ConfigureAwait(false);

                return FinalizeDicomDirectoryLoad(df, reader, dirObserver, result);
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }
#endif

        /// <summary>
        /// Method to call before performing the actual saving.
        /// </summary>
        protected override void OnSave()
        {
            if (RootDirectoryRecord == null) throw new InvalidOperationException("No DICOM files added, cannot save DICOM directory");

            _directoryRecordSequence.Items.Clear();
            var calculator = new DicomWriteLengthCalculator(FileMetaInfo.TransferSyntax, DicomWriteOptions.Default);

            //Add the offset for the Directory Record sequence tag itself
            if (FileMetaInfo.TransferSyntax.IsExplicitVR)
            {
                _fileOffset = 128 + calculator.Calculate(FileMetaInfo) + calculator.Calculate(Dataset);
                _fileOffset += 2; // vr
                _fileOffset += 2; // padding
                _fileOffset += 4; // length
            }
            else
            {
                _fileOffset = 128 + 4 + calculator.Calculate(FileMetaInfo) + calculator.Calculate(Dataset);

                _fileOffset += 4; //sequence element tag
                _fileOffset += 4; //length
            }

            AddDirectoryRecordsToSequenceItem(RootDirectoryRecord);

            if (RootDirectoryRecord != null)
            {
                CalculateOffsets(calculator);

                SetOffsets(RootDirectoryRecord);

                Dataset.AddOrUpdate<uint>(
                    DicomTag.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity,
                    RootDirectoryRecord.Offset);

                var lastRoot = RootDirectoryRecord;

                while (lastRoot.NextDirectoryRecord != null) lastRoot = lastRoot.NextDirectoryRecord;

                Dataset.AddOrUpdate<uint>(DicomTag.OffsetOfTheLastDirectoryRecordOfTheRootDirectoryEntity, lastRoot.Offset);
            }
            else
            {
                Dataset.AddOrUpdate<uint>(DicomTag.OffsetOfTheFirstDirectoryRecordOfTheRootDirectoryEntity, 0);
                Dataset.AddOrUpdate<uint>(DicomTag.OffsetOfTheLastDirectoryRecordOfTheRootDirectoryEntity, 0);
            }
        }

        #endregion

        #region Calculation Methods

        private void CalculateOffsets(DicomWriteLengthCalculator calculator)
        {
            foreach (var item in Dataset.Get<DicomSequence>(DicomTag.DirectoryRecordSequence))
            {
                var record = item as DicomDirectoryRecord;
                if (record == null) throw new InvalidOperationException("Unexpected type for directory record: " + item.GetType());

                record.Offset = _fileOffset;

                _fileOffset += 4 + 4; //Sequence item tag;

                _fileOffset += calculator.Calculate(record);

                _fileOffset += 4 + 4; // Sequence Item Delimitation Item
            }

            _fileOffset += 4 + 4; // Sequence Delimitation Item
        }

        private void SetOffsets(DicomDirectoryRecord record)
        {
            if (record.NextDirectoryRecord != null)
            {
                record.AddOrUpdate<uint>(DicomTag.OffsetOfTheNextDirectoryRecord, record.NextDirectoryRecord.Offset);
                SetOffsets(record.NextDirectoryRecord);
            }
            else
            {
                record.AddOrUpdate<uint>(DicomTag.OffsetOfTheNextDirectoryRecord, 0);
            }

            if (record.LowerLevelDirectoryRecord != null)
            {
                record.AddOrUpdate<uint>(
                    DicomTag.OffsetOfReferencedLowerLevelDirectoryEntity,
                    record.LowerLevelDirectoryRecord.Offset);
                SetOffsets(record.LowerLevelDirectoryRecord);
            }
            else
            {
                record.AddOrUpdate<uint>(DicomTag.OffsetOfReferencedLowerLevelDirectoryEntity, 0);
            }
        }

        #endregion

        #region File system creator Methods

        /// <summary>
        /// Add new file to DICOM directory.
        /// </summary>
        /// <param name="dicomFile">DICOM file to add.</param>
        /// <param name="recordTypeName">Record type name.</param>
        /// <param name="referencedFileId">Referenced file ID.</param>
        public void AddFile(DicomFile dicomFile, string recordTypeName, string referencedFileId = "")
        {
            if (dicomFile == null) throw new ArgumentNullException(nameof(dicomFile));

            this.AddNewRecord(dicomFile.FileMetaInfo, dicomFile.Dataset, referencedFileId, recordTypeName);
        }

        private void AddNewRecord(DicomFileMetaInformation metaFileInfo, DicomDataset dataset, string referencedFileId, string recordTypeName)
        {
            var patientRecord = this.CreatePatientRecord(dataset);
            var studyRecord = this.CreateStudyRecord(dataset, patientRecord);
            var seriesRecord = this.CreateSeriesRecord(dataset, studyRecord);

            switch (recordTypeName)
            {
                case RecordTypeName.Image:
                    CreateImageRecord(metaFileInfo, dataset, seriesRecord, referencedFileId);
                    break;
                case RecordTypeName.SrDocument:
                    CreateSrDocumentRecord(metaFileInfo, dataset, seriesRecord, referencedFileId);
                    break;
            }
            
        }

        private DicomDirectoryRecord FindFirstChildRecord(string recordTypeName, object parent)
        {
            DicomDirectoryRecordCollection lowerLevelRecords = null;
            DicomDirectoryRecord firstChildRecord = null;

            if (parent is DicomDirectory)
            {
                lowerLevelRecords = ((DicomDirectory) parent).RootDirectoryRecordCollection;
            }
            else if (parent is DicomDirectoryRecord)
            {
                lowerLevelRecords = ((DicomDirectoryRecord) parent).LowerLevelDirectoryRecordCollection;
            }

            if (lowerLevelRecords != null)
            {
                firstChildRecord = lowerLevelRecords.FirstOrDefault((lowerLevelRecord) =>
                {
                    bool result = (recordTypeName == lowerLevelRecord.Get<string>(DicomTag.DirectoryRecordType, null));
                    return result;
                });
            }

            return firstChildRecord;
        }

        private void CreateSrDocumentRecord(
            DicomFileMetaInformation metaFileInfo,
            DicomDataset dataset,
            DicomDirectoryRecord seriesRecord,
            string referencedFileId)
        {
            var currentSrDocument = FindFirstChildRecord(RecordTypeName.SrDocument, seriesRecord);
            var srDocumentInstanceUid = dataset.Get<string>(DicomTag.SOPInstanceUID);

            while (currentSrDocument != null)
            {
                if (currentSrDocument.Get<string>(DicomTag.ReferencedSOPInstanceUIDInFile) == srDocumentInstanceUid)
                {
                    return;
                }

                var nextRecord = currentSrDocument.NextDirectoryRecord;
                if (nextRecord != null && (RecordTypeName.SrDocument == nextRecord.Get<string>(DicomTag.DirectoryRecordType)))
                {
                    currentSrDocument = currentSrDocument.NextDirectoryRecord;
                }
                else
                {
                    //no more sr document records, break the loop
                    break;
                }
            }

            var newSrDocument = CreateRecordSequenceItem(DicomDirectoryRecordType.SRDocument, dataset);
            newSrDocument.AddOrUpdate(DicomTag.ReferencedFileID, referencedFileId);
            newSrDocument.AddOrUpdate(DicomTag.ReferencedSOPClassUIDInFile, metaFileInfo.MediaStorageSOPClassUID.UID);
            newSrDocument.AddOrUpdate(DicomTag.ReferencedSOPInstanceUIDInFile, metaFileInfo.MediaStorageSOPInstanceUID.UID);
            newSrDocument.AddOrUpdate(DicomTag.ReferencedTransferSyntaxUIDInFile, metaFileInfo.TransferSyntax.UID);
            newSrDocument.AddOrUpdate(DicomTag.CompletionFlag, "COMPLETE");
            newSrDocument.AddOrUpdate(DicomTag.VerificationFlag, "UNVERIFIED");

            if (currentSrDocument != null)
            {
                //sr document not found under series record
                currentSrDocument.NextDirectoryRecord = newSrDocument;
            }
            else
            {
                //no sr document record found under series record
                seriesRecord.LowerLevelDirectoryRecord = newSrDocument;
            }
        }

        private void CreateImageRecord(
            DicomFileMetaInformation metaFileInfo,
            DicomDataset dataset,
            DicomDirectoryRecord seriesRecord,
            string referencedFileId)
        {
            var currentImage = FindFirstChildRecord(RecordTypeName.Image, seriesRecord);
            var imageInstanceUid = dataset.Get<string>(DicomTag.SOPInstanceUID);


            while (currentImage != null)
            {
                if (currentImage.Get<string>(DicomTag.ReferencedSOPInstanceUIDInFile) == imageInstanceUid)
                {
                    return;
                }

                var nextRecord = currentImage.NextDirectoryRecord;
                if (nextRecord != null && (RecordTypeName.Image == nextRecord.Get<string>(DicomTag.DirectoryRecordType)))
                {
                    currentImage = currentImage.NextDirectoryRecord;
                }
                else
                {
                    //no more image records, break the loop
                    break;
                }
            }
            var newImage = CreateRecordSequenceItem(DicomDirectoryRecordType.Image, dataset);
            newImage.AddOrUpdate(DicomTag.ReferencedFileID, referencedFileId);
            newImage.AddOrUpdate(DicomTag.ReferencedSOPClassUIDInFile, metaFileInfo.MediaStorageSOPClassUID.UID);
            newImage.AddOrUpdate(DicomTag.ReferencedSOPInstanceUIDInFile, metaFileInfo.MediaStorageSOPInstanceUID.UID);
            newImage.AddOrUpdate(DicomTag.ReferencedTransferSyntaxUIDInFile, metaFileInfo.TransferSyntax.UID);

            if (currentImage != null)
            {
                //image not found under series record
                currentImage.NextDirectoryRecord = newImage;
            }
            else
            {
                //no image record found under series record
                seriesRecord.LowerLevelDirectoryRecord = newImage;
            }
        }

        private DicomDirectoryRecord CreateSeriesRecord(DicomDataset dataset, DicomDirectoryRecord studyRecord)
        {
            var currentSeries = studyRecord.LowerLevelDirectoryRecord;
            var seriesInstanceUid = dataset.Get<string>(DicomTag.SeriesInstanceUID);


            while (currentSeries != null)
            {
                if (currentSeries.Get<string>(DicomTag.SeriesInstanceUID) == seriesInstanceUid)
                {
                    return currentSeries;
                }

                if (currentSeries.NextDirectoryRecord != null)
                {
                    currentSeries = currentSeries.NextDirectoryRecord;
                }
                else
                {
                    //no more series records, break the loop
                    break;
                }
            }

            var newSeries = CreateRecordSequenceItem(DicomDirectoryRecordType.Series, dataset);
            if (currentSeries != null)
            {
                //series not found under study record
                currentSeries.NextDirectoryRecord = newSeries;
            }
            else
            {
                //no series record found under study record
                studyRecord.LowerLevelDirectoryRecord = newSeries;
            }
            return newSeries;
        }

        private DicomDirectoryRecord CreateStudyRecord(DicomDataset dataset, DicomDirectoryRecord patientRecord)
        {
            var currentStudy = patientRecord.LowerLevelDirectoryRecord;
            var studyInstanceUid = dataset.Get<string>(DicomTag.StudyInstanceUID);


            while (currentStudy != null)
            {
                if (currentStudy.Get<string>(DicomTag.StudyInstanceUID) == studyInstanceUid)
                {
                    return currentStudy;
                }

                if (currentStudy.NextDirectoryRecord != null)
                {
                    currentStudy = currentStudy.NextDirectoryRecord;
                }
                else
                {
                    //no more study records, break the loop
                    break;
                }
            }
            var newStudy = CreateRecordSequenceItem(DicomDirectoryRecordType.Study, dataset);
            if (currentStudy != null)
            {
                //study not found under patient record
                currentStudy.NextDirectoryRecord = newStudy;
            }
            else
            {
                //no studies record found under patient record
                patientRecord.LowerLevelDirectoryRecord = newStudy;
            }
            return newStudy;
        }

        private DicomDirectoryRecord CreatePatientRecord(DicomDataset dataset)
        {
            var patientId = dataset.Get(DicomTag.PatientID, string.Empty);
            var patientName = dataset.Get(DicomTag.PatientName, string.Empty);

            var currentPatient = RootDirectoryRecord;

            while (currentPatient != null)
            {
                var currPatId = currentPatient.Get(DicomTag.PatientID, string.Empty);
                var currPatName = currentPatient.Get(DicomTag.PatientName, string.Empty);

                if (currPatId == patientId && currPatName == patientName)
                {
                    return currentPatient;
                }

                if (currentPatient.NextDirectoryRecord != null)
                {
                    currentPatient = currentPatient.NextDirectoryRecord;
                }
                else
                {
                    //no more patient records, break the loop
                    break;
                }
            }

            var newPatient = CreateRecordSequenceItem(DicomDirectoryRecordType.Patient, dataset);
            if (currentPatient != null)
            {
                //patient not found under root record
                currentPatient.NextDirectoryRecord = newPatient;
            }
            else
            {
                //no patients record found under root record
                RootDirectoryRecord = newPatient;
            }

            return newPatient;
        }

        private DicomDirectoryRecord CreateRecordSequenceItem(DicomDirectoryRecordType recordType, DicomDataset dataset)
        {
            if (recordType == null) throw new ArgumentNullException(nameof(recordType));
            if (dataset == null) throw new ArgumentNullException(nameof(dataset));

            var sequenceItem = new DicomDirectoryRecord();

            //add record item attributes
            sequenceItem.Add<uint>(DicomTag.OffsetOfTheNextDirectoryRecord, 0);
            sequenceItem.Add<ushort>(DicomTag.RecordInUseFlag, 0xFFFF);
            sequenceItem.Add<uint>(DicomTag.OffsetOfReferencedLowerLevelDirectoryEntity, 0);
            sequenceItem.Add<string>(DicomTag.DirectoryRecordType, recordType.ToString());

            //copy the current dataset character set
            sequenceItem.Add(dataset.FirstOrDefault(d => d.Tag == DicomTag.SpecificCharacterSet));

            foreach (var tag in recordType.Tags)
            {
                if (dataset.Contains(tag))
                {
                    sequenceItem.Add(dataset.Get<DicomItem>(tag));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot find tag {tag} for record type {recordType}");
                }
            }

            return sequenceItem;
        }

        private static DicomDirectory FinalizeDicomDirectoryLoad(
            DicomDirectory df,
            DicomFileReader reader,
            DicomDirectoryReaderObserver dirObserver,
            DicomReaderResult result)
        {
            if (result == DicomReaderResult.Processing)
            {
                throw new DicomFileException(df, "Invalid read return state: {state}", result);
            }
            if (result == DicomReaderResult.Error)
            {
                return null;
            }
            df.IsPartial = result == DicomReaderResult.Stopped || result == DicomReaderResult.Suspended;

            df.Format = reader.FileFormat;

            df.Dataset.InternalTransferSyntax = reader.Syntax;

            df._directoryRecordSequence = df.Dataset.Get<DicomSequence>(DicomTag.DirectoryRecordSequence);
            df.RootDirectoryRecord = dirObserver.BuildDirectoryRecords();

            return df;
        }

        private void AddDirectoryRecordsToSequenceItem(DicomDirectoryRecord recordItem)
        {
            if (recordItem == null) return;

            _directoryRecordSequence.Items.Add(recordItem);
            if (recordItem.LowerLevelDirectoryRecord != null) AddDirectoryRecordsToSequenceItem(recordItem.LowerLevelDirectoryRecord);

            if (recordItem.NextDirectoryRecord != null) AddDirectoryRecordsToSequenceItem(recordItem.NextDirectoryRecord);
        }

        public DicomDirectoryRecord Find(DicomTag tagInRecord, object container, string tagValueInDicomFile)
        {
            DicomDirectoryRecord foundRecord = null;
            DicomDirectoryRecord previousRecord = null;

            Find(tagInRecord, container, tagValueInDicomFile, out foundRecord, out previousRecord);

            return foundRecord;
        }

        public void Find(DicomTag tagInRecord, object container, string tagValueInDicomFile, out DicomDirectoryRecord foundRecord, out DicomDirectoryRecord previousRecord)
        {
            foundRecord = null;
            previousRecord = null;
            DicomDirectoryRecordCollection lowerLevelRecords = null;

            if (container is DicomDirectory)
            {
                lowerLevelRecords = ((DicomDirectory) container).RootDirectoryRecordCollection;
            }
            else
            {
                lowerLevelRecords = ((DicomDirectoryRecord) container).LowerLevelDirectoryRecordCollection;
            }

            if (!string.IsNullOrEmpty(tagValueInDicomFile))
            {
                foreach (var lowerLevelRecord in lowerLevelRecords)
                {
                    string tagValueInRecord = lowerLevelRecord.Get<string>(tagInRecord, null);

                    if (tagValueInDicomFile == tagValueInRecord)
                    {
                        foundRecord = lowerLevelRecord;
                        break;
                    }

                    previousRecord = lowerLevelRecord;
                }
            }
        }

        public void Remove(DicomDirectoryRecord childRecord, DicomDirectoryRecord previousRecord, object parent)
        {
            if (previousRecord == null)
            {
                RemoveFirstChild(parent);
            }
            else
            {
                previousRecord.NextDirectoryRecord = childRecord.NextDirectoryRecord;
            }
        }

        private void RemoveFirstChild(object parent)
        {
            var record = parent as DicomDirectoryRecord;
            if (record != null)
            {
                record.LowerLevelDirectoryRecord = record.LowerLevelDirectoryRecord.NextDirectoryRecord;
                return;
            }

            var dicomDirectory = parent as DicomDirectory;
            if (dicomDirectory != null)
            {
                dicomDirectory.RootDirectoryRecord = dicomDirectory.RootDirectoryRecord.NextDirectoryRecord;
                return;
            }

            throw new Exception("Unsupported object type!");
        }

        #endregion
    }
}

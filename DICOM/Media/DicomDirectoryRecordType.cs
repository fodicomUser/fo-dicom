// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System.Collections.Generic;

namespace Dicom.Media
{
    public class RecordTypeName
    {
        public const string Patient = "PATIENT";
        public const string Study = "STUDY";
        public const string Series = "SERIES";
        public const string Image = "IMAGE";
        public const string RtDose = "RT DOSE";
        public const string RtStructureSet = "RT STRUCTURE SET";
        public const string RtPlan = "RT PLAN";
        public const string RtTreatRecord="RT TREAT RECORD";
        public const string Presentation = "PRESENTATION";
        public const string Waveform = "WAVEFORM";
        public const string SrDocument = "SR DOCUMENT";
        public const string KeyObjectDoc = "KEY OBJECT DOC";
        public const string Spectroscopy = "SPECTROSCOPY";
        public const string RawData = "RAW DATA";
        public const string Registration = "REGISTRATION";
        public const string Fiducial = "FIDUCIAL";
        public const string HangingProtocol = "HANGING PROTOCOL";
        public const string EncapDoc = "ENCAP DOC";
        public const string HL7StrucDoc = "HL7 STRUC DOC";
        public const string ValueMap = "VALUE MAP";
        public const string Stereometric = "STEREOMETRIC";
        public const string Palette = "PALETTE";
        public const string Implant = "IMPLANT";
        public const string ImplantGroup = "IMPLANT GROUP";
        public const string ImplantAssy = "IMPLANT ASSY";
        public const string Measurement = "MEASUREMENT";
        public const string Surface = "SURFACE";
        public const string SurfaceScan = "SURFACE SCAN";
        public const string Tract = "TRACT";
        public const string Assessment = "ASSESSMENT";
        public const string Private = "PRIVATE";
    }

    public class DicomDirectoryRecordType
    {
        #region Properties and Attributes

        private readonly string _recordName;

        private readonly ICollection<DicomTag> _tags = new HashSet<DicomTag>();

        public ICollection<DicomTag> Tags
        {
            get
            {
                return _tags;
            }
        }

        public static readonly DicomDirectoryRecordType Patient = new DicomDirectoryRecordType(RecordTypeName.Patient);

        public static readonly DicomDirectoryRecordType Study = new DicomDirectoryRecordType(RecordTypeName.Study);

        public static readonly DicomDirectoryRecordType Series = new DicomDirectoryRecordType(RecordTypeName.Series);

        public static readonly DicomDirectoryRecordType Image = new DicomDirectoryRecordType(RecordTypeName.Image);

        public static readonly DicomDirectoryRecordType SRDocument = new DicomDirectoryRecordType(RecordTypeName.SrDocument);

        #endregion

        #region Initialization

        public DicomDirectoryRecordType(string recordName)
        {
            _recordName = recordName;

            switch (recordName)
            {
                case RecordTypeName.Patient:
                    _tags.Add(DicomTag.PatientID);
                    _tags.Add(DicomTag.PatientName);
                    _tags.Add(DicomTag.PatientBirthDate);
                    _tags.Add(DicomTag.PatientSex);
                    break;
                case RecordTypeName.Study:
                    _tags.Add(DicomTag.StudyInstanceUID);
                    _tags.Add(DicomTag.StudyID);
                    _tags.Add(DicomTag.StudyDate);
                    _tags.Add(DicomTag.StudyTime);
                    _tags.Add(DicomTag.AccessionNumber);
                    _tags.Add(DicomTag.StudyDescription);
                    break;
                case RecordTypeName.Series:
                    _tags.Add(DicomTag.SeriesInstanceUID);
                    _tags.Add(DicomTag.Modality);
                    _tags.Add(DicomTag.SeriesDate);
                    _tags.Add(DicomTag.SeriesTime);
                    _tags.Add(DicomTag.SeriesNumber);
                    _tags.Add(DicomTag.SeriesDescription);
                    break;
                case RecordTypeName.Image:
                    _tags.Add(DicomTag.InstanceNumber);
                    break;
                case RecordTypeName.SrDocument:
                    _tags.Add(DicomTag.InstanceNumber);
                    break;
                default:
                    break;
            }
        }

        #endregion

        public override string ToString()
        {
            return _recordName;
        }
    }
}
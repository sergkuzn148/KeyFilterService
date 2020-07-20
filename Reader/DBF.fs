module DBF

open System
open System.IO

type FileType =
    | FoxBASE = 0x02
    | FoxBASEDBaseIIIPlusNoMemo = 0x03
    | VisualFoxPro = 0x30
    | VisualFoxProAutoIncrement = 0x31
    | VisualFoxProWithVarchar = 0x32
    | DBaseIVSQLTableFilesNoMemo = 0x43
    | DBaseIVSQLSystemFilesNoMemo = 0x63
    | FoxBASEDBaseIIIPlusWithMemo = 0x83
    | DBaseIVWithMemo = 0x8B
    | DBaseIVSQLTableFilesWithMemo = 0xCB
    | FoxPro2xWithMemo = 0xF5
    | HiPerSixWithSMTMemo = 0xE5
    | FoxBase = 0xFB

[<Flags>]
type TableFlag =
    | CDX = 0x01
    | Memo = 0x02
    | DBC = 0x04

type FieldSubrecord = {
    Name: char[];
}

type Header = {
    FileType: FileType;
    LastUpdate: DateTime;
    NumberOfRecords: int32;
    FirstDataRecordPosition: int32;
    LengthOfDataRecord: int32;
    TableFlag: TableFlag;
    CodePage: int32;
    FieldSubRecords: FieldSubrecord[];
    RecordTerminator: int32;
}
   
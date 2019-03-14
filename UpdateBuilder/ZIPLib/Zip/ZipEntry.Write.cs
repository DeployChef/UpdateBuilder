// ZipEntry.Write.cs
// ------------------------------------------------------------------
//
// Copyright (c)  2009 Dino Chiesa
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License. 
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// last saved (in emacs): 
// Time-stamp: <2009-August-25 12:45:57>
//
// ------------------------------------------------------------------
//
// This module defines logic for writing (saving) the ZipEntry into a
// zip file.
//
// 
// ------------------------------------------------------------------


using System;
using System.IO;
using RE = System.Text.RegularExpressions;

namespace UpdateBuilder.ZIPLib.Zip
{

    public partial class ZipEntry
    {

        internal void WriteCentralDirectoryEntry(Stream s)
        {
            _ConsAndWriteCentralDirectoryEntry(s);
        }


        private void _ConsAndWriteCentralDirectoryEntry(Stream s)
        {
            byte[] bytes = new byte[4096];
            int i = 0;
            // signature 
            bytes[i++] = (byte)(ZipConstants.ZipDirEntrySignature & 0x000000FF);
            bytes[i++] = (byte)((ZipConstants.ZipDirEntrySignature & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((ZipConstants.ZipDirEntrySignature & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((ZipConstants.ZipDirEntrySignature & 0xFF000000) >> 24);

            // Version Made By
            // workitem 7071
            // We must not overwrite the VersionMadeBy field when writing out a zip archive.
            // The VersionMadeBy tells the zip reader the meaning of the File attributes. 
            // Overwriting the VersionMadeBy will result in inconsistent metadata. 
            // Consider the scenario where the application opens and reads a zip file that had been created
            // on Linux. Then the app adds one file to the Zip archive, and saves it.
            // The file attributes for all the entries added on Linux will be significant 
            // for Linux.  Therefore the VersionMadeBy for those entries must not be changed.
            // Only the entries that are actually created on Windows NTFS should get the 
            // VersionMadeBy indicating Windows/NTFS.  
            bytes[i++] = (byte)(_VersionMadeBy & 0x00FF);
            bytes[i++] = (byte)((_VersionMadeBy & 0xFF00) >> 8);

            // Apparently we want to duplicate the extra field here; we cannot
            // simply zero it out and assume tools and apps will use the right one.

            ////Int16 extraFieldLengthSave = (short)(_EntryHeader[28] + _EntryHeader[29] * 256);
            ////_EntryHeader[28] = 0;
            ////_EntryHeader[29] = 0;

            // Version Needed, Bitfield, compression method, lastmod,
            // crc, compressed and uncompressed sizes, filename length and extra field length.
            // These are all present in the local file header, but they may be zero values there.
            // So we cannot just copy them. 

            Int16 versionNeededToExtract = (Int16)(_OutputUsesZip64.Value ? 45 : 20);

            bytes[i++] = (byte)(versionNeededToExtract & 0x00FF);
            bytes[i++] = (byte)((versionNeededToExtract & 0xFF00) >> 8);
            bytes[i++] = (byte)(_BitField & 0x00FF);
            bytes[i++] = (byte)((_BitField & 0xFF00) >> 8);

            bytes[i++] = (byte)(CompressionMethod & 0x00FF);
            bytes[i++] = (byte)((CompressionMethod & 0xFF00) >> 8);

#if AESCRYPTO
            if (Encryption == EncryptionAlgorithm.WinZipAes128 ||
            Encryption == EncryptionAlgorithm.WinZipAes256)
            {
                i -= 2;
                bytes[i++] = 0x63;
                bytes[i++] = 0;
            }
#endif

            bytes[i++] = (byte)(_TimeBlob & 0x000000FF);
            bytes[i++] = (byte)((_TimeBlob & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((_TimeBlob & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((_TimeBlob & 0xFF000000) >> 24);
            bytes[i++] = (byte)(_Crc32 & 0x000000FF);
            bytes[i++] = (byte)((_Crc32 & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((_Crc32 & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((_Crc32 & 0xFF000000) >> 24);

            int j = 0;
            if (_OutputUsesZip64.Value)
            {
                // CompressedSize (Int32) and UncompressedSize - all 0xFF
                for (j = 0; j < 8; j++)
                    bytes[i++] = 0xFF;
            }
            else
            {
                bytes[i++] = (byte)(_CompressedSize & 0x000000FF);
                bytes[i++] = (byte)((_CompressedSize & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((_CompressedSize & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((_CompressedSize & 0xFF000000) >> 24);

                bytes[i++] = (byte)(_UncompressedSize & 0x000000FF);
                bytes[i++] = (byte)((_UncompressedSize & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((_UncompressedSize & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((_UncompressedSize & 0xFF000000) >> 24);
            }

            byte[] FileNameBytes = _GetEncodedFileNameBytes();
            Int16 filenameLength = (Int16)FileNameBytes.Length;
            bytes[i++] = (byte)(filenameLength & 0x00FF);
            bytes[i++] = (byte)((filenameLength & 0xFF00) >> 8);


            // do this again because now we have real data
            _presumeZip64 = _OutputUsesZip64.Value;
            _Extra = ConsExtraField(true);

            Int16 extraFieldLength = (Int16)((_Extra == null) ? 0 : _Extra.Length);
            bytes[i++] = (byte)(extraFieldLength & 0x00FF);
            bytes[i++] = (byte)((extraFieldLength & 0xFF00) >> 8);

            // File (entry) Comment Length
            // the _CommentBytes private field was set during WriteHeader()
            int commentLength = (_CommentBytes == null) ? 0 : _CommentBytes.Length;

            // the size of our buffer defines the max length of the comment we can write
            if (commentLength + i > bytes.Length) commentLength = bytes.Length - i;
            bytes[i++] = (byte)(commentLength & 0x00FF);
            bytes[i++] = (byte)((commentLength & 0xFF00) >> 8);

            // Disk number start
            bytes[i++] = 0;
            bytes[i++] = 0;

            // internal file attrs
            // workitem 7801
            bytes[i++] = (byte)((_IsText) ? 1 : 0); // lo bit: filetype hint.  0=bin, 1=txt. 
            bytes[i++] = 0;

            // external file attrs
            // workitem 7071
            bytes[i++] = (byte)(_ExternalFileAttrs & 0x000000FF);
            bytes[i++] = (byte)((_ExternalFileAttrs & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((_ExternalFileAttrs & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((_ExternalFileAttrs & 0xFF000000) >> 24);

            // relative offset of local header
            if (_OutputUsesZip64.Value)
            {
                // Value==true means it used Zip64.  
                for (j = 0; j < 4; j++) bytes[i++] = 0xFF;
            }
            else
            {
                bytes[i++] = (byte)(_RelativeOffsetOfLocalHeader & 0x000000FF);
                bytes[i++] = (byte)((_RelativeOffsetOfLocalHeader & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((_RelativeOffsetOfLocalHeader & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((_RelativeOffsetOfLocalHeader & 0xFF000000) >> 24);
            }

            // actual filename 
            for (j = 0; j < filenameLength; j++)
                bytes[i + j] = FileNameBytes[j];
            i += j;

            // "Extra field"
            if (_Extra != null)
            {
                for (j = 0; j < extraFieldLength; j++)
                    bytes[i + j] = _Extra[j];
                i += j;
            }

            // file (entry) comment
            if (commentLength != 0)
            {
                // now actually write the comment itself into the byte buffer
                for (j = 0; (j < commentLength) && (i + j < bytes.Length); j++)
                    bytes[i + j] = _CommentBytes[j];
                i += j;
            }

            s.Write(bytes, 0, i);
        }


#if INFOZIP_UTF8
        static private bool FileNameIsUtf8(char[] FileNameChars)
        {
            bool isUTF8 = false;
            bool isUnicode = false;
            for (int j = 0; j < FileNameChars.Length; j++)
            {
                byte[] b = System.BitConverter.GetBytes(FileNameChars[j]);
                isUnicode |= (b.Length != 2);
                isUnicode |= (b[1] != 0);
                isUTF8 |= ((b[0] & 0x80) != 0);
            }

            return isUTF8;
        }
#endif


        private byte[] ConsExtraField(bool forCentralDirectory)
        {
            var listOfBlocks = new System.Collections.Generic.List<byte[]>();
            byte[] block;

            // Always emit an extra field with zip64 information.
            // Later, if we don't need it, we'll set the header ID to rubbish and
            // the data will be ignored.  This results in additional overhead metadata
            // in the zip file, but it will be small in comparison to the entry data.
            if (_zipfile._zip64 != Zip64Option.Never)
            {
                // add extra field for zip64 here
                // workitem 7924
                int sz = 4 + (forCentralDirectory ? 28 : 16);
                block = new byte[sz];
                int i = 0;

                if (_presumeZip64)
                {
                    // HeaderId = always use zip64 extensions.
                    block[i++] = 0x01;
                    block[i++] = 0x00;
                }
                else
                {
                    // HeaderId = dummy data now, maybe set to 0x0001 (ZIP64) later.
                    block[i++] = 0x99;
                    block[i++] = 0x99;
                }

                // DataSize
                block[i++] = (byte)(sz - 4);  // decimal 28 or 16  (workitem 7924)
                block[i++] = 0x00;

                // The actual metadata - we may or may not have real values yet...

                // uncompressed size
                Array.Copy(BitConverter.GetBytes(_UncompressedSize), 0, block, i, 8);
                i += 8;
                // compressed size
                Array.Copy(BitConverter.GetBytes(_CompressedSize), 0, block, i, 8);

                // workitem 7924 - only include this if the "extra" field is for use in the central directory.
                // It is unnecessary and not useful for local header; makes WinZip choke.
                if (forCentralDirectory)
                {
                    i += 8;
                    // relative offset
                    Array.Copy(BitConverter.GetBytes(_RelativeOffsetOfLocalHeader), 0, block, i, 8);
                    i += 8;
                    // starting disk number
                    Array.Copy(BitConverter.GetBytes(0), 0, block, i, 4);
                }
                listOfBlocks.Add(block);
            }


#if AESCRYPTO
            if (Encryption == EncryptionAlgorithm.WinZipAes128 ||
                Encryption == EncryptionAlgorithm.WinZipAes256)
            {
                block = new byte[4 + 7];
                int i = 0;
                // extra field for WinZip AES 
                // header id
                block[i++] = 0x01;
                block[i++] = 0x99;

                // data size
                block[i++] = 0x07;
                block[i++] = 0x00;

                // vendor number
                block[i++] = 0x01;  // AE-1 - means "Verify CRC"
                block[i++] = 0x00;

                // vendor id "AE"
                block[i++] = 0x41;
                block[i++] = 0x45;

                // key strength
                block[i] = 0xFF;
                if (_KeyStrengthInBits == 128)
                    block[i] = 1;
                if (_KeyStrengthInBits == 256)
                    block[i] = 3;
                i++;

                // actual compression method
                block[i++] = (byte)(_CompressionMethod & 0x00FF);
                block[i++] = (byte)(_CompressionMethod & 0xFF00);

                listOfBlocks.Add(block);
            }
#endif

            if (_ntfsTimesAreSet && _emitNtfsTimes)
            {
                block = new byte[32 + 4];
                // HeaderId   2 bytes    0x000a == NTFS times
                // Datasize   2 bytes    32
                // reserved   4 bytes    ?? don't care 
                // timetag    2 bytes    0x0001 == NTFS time
                // size       2 bytes    24 == 8 bytes each for ctime, mtime, atime
                // mtime      8 bytes    win32 ticks since win32epoch
                // atime      8 bytes    win32 ticks since win32epoch
                // ctime      8 bytes    win32 ticks since win32epoch
                int i = 0;
                // extra field for NTFS times
                // header id
                block[i++] = 0x0a;
                block[i++] = 0x00;

                // data size
                block[i++] = 32;
                block[i++] = 0;

                i += 4; // reserved

                // time tag
                block[i++] = 0x01;
                block[i++] = 0x00;

                // data size (again)
                block[i++] = 24;
                block[i++] = 0;

                Int64 z = _Mtime.ToFileTime();
                Array.Copy(BitConverter.GetBytes(z), 0, block, i, 8);
                i += 8;
                z = _Atime.ToFileTime();
                Array.Copy(BitConverter.GetBytes(z), 0, block, i, 8);
                i += 8;
                z = _Ctime.ToFileTime();
                Array.Copy(BitConverter.GetBytes(z), 0, block, i, 8);
                i += 8;

                listOfBlocks.Add(block);
            }

            if (_ntfsTimesAreSet && _emitUnixTimes)
            {
                int len = 5 + 4;
                if (!forCentralDirectory) len += 8;

                block = new byte[len];
                // local form:
                // --------------
                // HeaderId   2 bytes    0x5455 == unix timestamp
                // Datasize   2 bytes    13 
                // flags      1 byte     7 (low three bits all set)
                // mtime      4 bytes    seconds since unix epoch
                // atime      4 bytes    seconds since unix epoch
                // ctime      4 bytes    seconds since unix epoch
                //
                // central directory form:
                //---------------------------------
                // HeaderId   2 bytes    0x5455 == unix timestamp
                // Datasize   2 bytes    5
                // flags      1 byte     7 (low three bits all set)
                // mtime      4 bytes    seconds since unix epoch
                //
                int i = 0;
                // extra field for "unix" times
                // header id
                block[i++] = 0x55;
                block[i++] = 0x54;

                // data size
                block[i++] = unchecked((byte)(len - 4));
                block[i++] = 0;

                // flags
                block[i++] = 0x07;

                Int32 z = unchecked((int)((_Mtime - _unixEpoch).TotalSeconds));
                Array.Copy(BitConverter.GetBytes(z), 0, block, i, 4);
                i += 4;
                if (!forCentralDirectory)
                {
                    z = unchecked((int)((_Atime - _unixEpoch).TotalSeconds));
                    Array.Copy(BitConverter.GetBytes(z), 0, block, i, 4);
                    i += 4;
                    z = unchecked((int)((_Ctime - _unixEpoch).TotalSeconds));
                    Array.Copy(BitConverter.GetBytes(z), 0, block, i, 4);
                    i += 4;
                }
                listOfBlocks.Add(block);
            }


            // inject other blocks here...


            // concatenate any blocks we've got: 
            byte[] aggregateBlock = null;
            if (listOfBlocks.Count > 0)
            {
                int totalLength = 0;
                int i, current = 0;
                for (i = 0; i < listOfBlocks.Count; i++)
                    totalLength += listOfBlocks[i].Length;
                aggregateBlock = new byte[totalLength];
                for (i = 0; i < listOfBlocks.Count; i++)
                {
                    System.Array.Copy(listOfBlocks[i], 0, aggregateBlock, current, listOfBlocks[i].Length);
                    current += listOfBlocks[i].Length;
                }
            }

            return aggregateBlock;
        }



        // workitem 6513: when writing, use alt encoding only when ibm437 will not do
        private System.Text.Encoding GenerateCommentBytes()
        {
            _CommentBytes = ibm437.GetBytes(_Comment);
            // need to use this form of GetString() for .NET CF
            string s1 = ibm437.GetString(_CommentBytes, 0, _CommentBytes.Length);
            if (s1 == _Comment)
                return ibm437;
            else
            {
                _CommentBytes = _provisionalAlternateEncoding.GetBytes(_Comment);
                return _provisionalAlternateEncoding;
            }
        }


        // workitem 6513
        private byte[] _GetEncodedFileNameBytes()
        {
            // here, we need to flip the backslashes to forward-slashes, 
            // also, we need to trim the \\server\share syntax from any UNC path.
            // and finally, we need to remove any leading .\

            string SlashFixed = FileName.Replace("\\", "/");
            string s1 = null;
            if ((_TrimVolumeFromFullyQualifiedPaths) && (FileName.Length >= 3)
                && (FileName[1] == ':') && (SlashFixed[2] == '/'))
            {
                // trim off volume letter, colon, and slash
                s1 = SlashFixed.Substring(3);
            }
            else if ((FileName.Length >= 4)
                     && ((SlashFixed[0] == '/') && (SlashFixed[1] == '/')))
            {
                int n = SlashFixed.IndexOf('/', 2);
                //System.Console.WriteLine("input Path '{0}'", FileName);
                //System.Console.WriteLine("xformed: '{0}'", SlashFixed);
                //System.Console.WriteLine("third slash: {0}\n", n);
                if (n == -1)
                    throw new ArgumentException("The path for that entry appears to be badly formatted");
                s1 = SlashFixed.Substring(n + 1);
            }
            else if ((FileName.Length >= 3)
                     && ((SlashFixed[0] == '.') && (SlashFixed[1] == '/')))
            {
                // trim off dot and slash
                s1 = SlashFixed.Substring(2);
            }
            else
            {
                s1 = SlashFixed;
            }

            // workitem 6513: when writing, use the alternative encoding only when ibm437 will not do.
            byte[] result = ibm437.GetBytes(s1);
            // need to use this form of GetString() for .NET CF
            string s2 = ibm437.GetString(result, 0, result.Length);
            _CommentBytes = null;
            if (s2 == s1)
            {
                // file can be encoded with ibm437, now try comment

                // case 1: no comment.  use ibm437
                if (_Comment == null || _Comment.Length == 0)
                {
                    _actualEncoding = ibm437;
                    return result;
                }

                // there is a comment.  Get the encoded form.
                System.Text.Encoding commentEncoding = GenerateCommentBytes();

                // case 2: if the comment also uses 437, we're good. 
                if (commentEncoding.CodePage == 437)
                {
                    _actualEncoding = ibm437;
                    return result;
                }

                // case 3: comment requires non-437 code page.  Use the same
                // code page for the filename.
                _actualEncoding = commentEncoding;
                result = commentEncoding.GetBytes(s1);
                return result;
            }
            else
            {
                // Cannot encode with ibm437 safely.
                // Therefore, use the provisional encoding
                result = _provisionalAlternateEncoding.GetBytes(s1);
                if (_Comment != null && _Comment.Length != 0)
                {
                    _CommentBytes = _provisionalAlternateEncoding.GetBytes(_Comment);
                }

                _actualEncoding = _provisionalAlternateEncoding;
                return result;
            }
        }


        private bool WantReadAgain()
        {
            if (_UncompressedSize < 0x10) return false;
            if (_CompressionMethod == 0x00) return false;
            if (_CompressedSize < _UncompressedSize) return false;
            if (ForceNoCompression) return false;
            if (this._Source == ZipEntrySource.Stream && !this._sourceStream.CanSeek) return false;

#if AESCRYPTO
            if (_aesCrypto != null && (CompressedSize - _aesCrypto.SizeOfEncryptionMetadata) <= UncompressedSize + 0x10) return false;
#endif

            if (_zipCrypto != null && (CompressedSize - 12) <= UncompressedSize) return false;

            // finally, check the delegate 
            if (WillReadTwiceOnInflation != null)
                return WillReadTwiceOnInflation(_UncompressedSize, _CompressedSize, FileName);

            return true;
        }

        // heuristic - if the filename is one of a known list of non-compressible files, 
        // return false. else true.  We apply this by just checking the extension. 
        // (?i) = use case-insensitive matching
        private static RE.Regex _IncompressibleRegex = new RE.Regex("(?i)^(.+)\\.(mp3|png|docx|xlsx|pptx|jpg|zip)$");
        private static bool SeemsCompressible(string filename)
        {
            return !_IncompressibleRegex.IsMatch(filename);
        }


        private bool DefaultWantCompression()
        {
            if (_LocalFileName != null)
                return SeemsCompressible(_LocalFileName);

            if (_FileNameInArchive != null)
                return SeemsCompressible(_FileNameInArchive);

            return true;
        }



        private void FigureCompressionMethodForWriting(int cycle)
        {
            // if we've already tried with compression... turn it off this time
            if (cycle > 1)
            {
                _CompressionMethod = 0x0;
            }
            // compression for directories = 0x00 (No Compression)
            else if (IsDirectory)
            {
                _CompressionMethod = 0x0;
            }
            else if (__FileDataPosition != -1)
            {
                // If at this point, __FileDataPosition is non-zero, that means we've read this
                // entry from an existing zip archive. 
                // 
                // In this case, we just keep the existing file data and metadata (including
                // CompressionMethod, CRC, compressed size, uncompressed size, etc).
                // 
                // All those member variables have been set during read! 
                // 
            }
            else
            {
                // If __FileDataPosition is zero, then that means we will get the data
                // from a file or stream.

                // It is never possible to compress a zero-length file, so we check for 
                // this condition. 

                if (this._Source == ZipEntrySource.Stream)
                {
                    // workitem 7742
                    if (_sourceStream != null && _sourceStream.CanSeek)
                    {
                        // Length prop will throw if CanSeek is false
                        long fileLength = _sourceStream.Length;
                        if (fileLength == 0)
                        {
                            _CompressionMethod = 0x00;
                            return;
                        }
                    }
                }
                else
                {
                    // special case zero-length files
                    FileInfo fi = new FileInfo(LocalFileName);
                    long fileLength = fi.Length;
                    if (fileLength == 0)
                    {
                        _CompressionMethod = 0x00;
                        return;
                    }
                }

                if (_ForceNoCompression)
                {
                    _CompressionMethod = 0x00;
                    return;
                }

                // Ok, we're getting the data to be compressed from a non-zero length file
                // or stream.  In that case we check the callback to see if the app
                // wants to tell us whether to compress or not.  

                if (WantCompression != null)
                {
                    _CompressionMethod = (short)(WantCompression(LocalFileName, _FileNameInArchive)
                                                 ? 0x08 : 0x00);
                    return;
                }

                // if there is no callback set, we use the default behavior.
                _CompressionMethod = (short)(DefaultWantCompression()
                                             ? 0x08 : 0x00);
                //Console.WriteLine("DefaultWantCompression: {0}", _CompressionMethod);
                return;
            }
        }



        // write the header info for an entry
        private void WriteHeader(Stream s, int cycle)
        {
            int j = 0;

            // remember the offset, within the output stream, of this particular entry header
            var counter = s as CountingStream;
            // workitem 8098: ok (output)
            _RelativeOffsetOfLocalHeader = (counter != null)
                ? counter.BytesWritten
                : s.Position;

            byte[] bytes = new byte[512];  // large enough for looooong filenames (MAX_PATH == 260)

            int i = 0;
            // signature
            bytes[i++] = (byte)(ZipConstants.ZipEntrySignature & 0x000000FF);
            bytes[i++] = (byte)((ZipConstants.ZipEntrySignature & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((ZipConstants.ZipEntrySignature & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((ZipConstants.ZipEntrySignature & 0xFF000000) >> 24);

            // validate the ZIP64 usage
            if (_zipfile._zip64 == Zip64Option.Never && (uint)_RelativeOffsetOfLocalHeader >= 0xFFFFFFFF)
                throw new ZipException("Offset within the zip archive exceeds 0xFFFFFFFF. Consider setting the UseZip64WhenSaving property on the ZipFile instance.");


            // Design notes for ZIP64:

            // The specification says that the header must include the Compressed and
            // Uncompressed sizes, as well as the CRC32 value.  When creating a zip via
            // streamed processing, these quantities are not known until after the compression
            // is done.  Thus, a typical way to do it is to insert zeroes for these quantities,
            // then do the compression, then seek back to insert the appropriate values, then
            // seek forward to the end of the file data.

            // There is also the option of using bit 3 in the GP bitfield - to specify that
            // there is a data descriptor after the file data containing these three
            // quantities.

            // This works when the size of the quantities is known, either 32-bits or 64 bits as 
            // with the ZIP64 extensions.  

            // With Zip64, the 4-byte fields are set to 0xffffffff, and there is a
            // corresponding data block in the "extra field" that contains the actual
            // Compressed, uncompressed sizes.  (As well as an additional field, the "Relative
            // Offset of Local Header")

            // The problem is when the app desires to use ZIP64 extensions optionally, only
            // when necessary.  Suppose the library assumes no zip64 extensions when writing
            // the header, then after compression finds that the size of the data requires
            // zip64.  At this point, the header, already written to the file, won't have the
            // necessary data block in the "extra field".  The size of the entry header is
            // fixed, so it is not possible to just "add on" the zip64 data block after
            // compressing the file.  On the other hand, always using zip64 will break
            // interoperability with many other systems and apps.

            // The approach we take is to insert a 32-byte dummy data block in the extra field,
            // whenever zip64 is to be used "as necessary". This data block will get the actual
            // zip64 HeaderId and zip64 metadata if necessary.  If not necessary, the data
            // block will get a meaningless HeaderId (0x1111), and will be filled with zeroes.

            // When zip64 is actually in use, we also need to set the VersionNeededToExtract
            // field to 45.
            //

            // There is one additional wrinkle: using zip64 as necessary conflicts with output
            // to non-seekable devices.  The header is emitted and must indicate whether zip64
            // is in use, before we know if zip64 is necessary.  Because there is no seeking,
            // the header can never be changed.  Therefore, on non-seekable devices,
            // Zip64Option.AsNecessary is the same as Zip64Option.Always.

            // version needed- see AppNote.txt.
            // need v5.1 for PKZIP strong encryption, or v2.0 for no encryption or for PK
            // encryption, 4.5 for zip64.  We may reset this later, as necessary or zip64.

            _presumeZip64 = (_zipfile._zip64 == Zip64Option.Always || (_zipfile._zip64 == Zip64Option.AsNecessary && !s.CanSeek));
            Int16 VersionNeededToExtract = (Int16)(_presumeZip64 ? 45 : 20);

            // (i==4)
            bytes[i++] = (byte)(VersionNeededToExtract & 0x00FF);
            bytes[i++] = (byte)((VersionNeededToExtract & 0xFF00) >> 8);

            // get byte array including any encoding
            // workitem 6513
            byte[] FileNameBytes = _GetEncodedFileNameBytes();
            Int16 filenameLength = (Int16)FileNameBytes.Length;

            // general purpose bitfield
            // In the current implementation, this library uses only these bits 
            // in the GP bitfield:
            //  bit 0 = if set, indicates the entry is encrypted
            //  bit 3 = if set, indicates the CRC, C and UC sizes follow the file data.
            //  bit 6 = strong encryption 
            //  bit 11 = UTF-8 encoding is used in the comment and filename

            _BitField = (Int16)((UsesEncryption) ? 1 : 0);

            // workitem 7941: WinZip does not set this when using AES.
            // this "Strong Encryption" is a PKWare Strong encryption thing.
            //             if (UsesEncryption && (IsStrong(Encryption)))
            //                 _BitField |= 0x0020;

            // set the UTF8 bit if necessary
            if (ActualEncoding.CodePage == System.Text.Encoding.UTF8.CodePage) _BitField |= 0x0800;

            // The PKZIP spec says that if bit 3 is set (0x0008) in the General Purpose BitField,
            // then the CRC, Compressed size, and uncompressed size are written directly after the
            // file data.   
            // 
            // Those 3 quantities are not knowable until after the compression is done. Yet they
            // are required to be in the header.  Normally, we'd 
            //  - write the header, using zeros for these quantities
            //  - compress the data, and incidentally compute these quantities.
            //  - seek back and write the correct values them into the header. 
            //
            // This is nice because it is simpler and less error prone to read the zip file.
            //
            // But if seeking in the output stream is not possible, then we need to set the
            // appropriate bitfield and emit these quantities after the compressed file data in
            // the output.

            // workitem 7216 - having trouble formatting a zip64 file that is readable by WinZip.
            // not sure why!  What I found is that setting bit 3 and following all the implications,
            // the zip64 file is readable by WinZip 12. and Perl's  IO::Compress::Zip . 
            // Perl takes an interesting approach - it always sets bit 3 if ZIP64 in use. 
            // I do the same, and it gives better compatibility with WinZip 12.

            // workitem 7924 - don't need this for WinZip compat any longer.
            //if (!s.CanSeek || _presumeZip64)
            if (!s.CanSeek)
                _BitField |= 0x0008;

            // (i==6)
            bytes[i++] = (byte)(_BitField & 0x00FF);
            bytes[i++] = (byte)((_BitField & 0xFF00) >> 8);

            // Here, we want to set values for Compressed Size, Uncompressed Size, and CRC.  If
            // we have __FileDataPosition as not -1 (zero is a valid FDP), then that means we
            // are reading this zip entry from a zip file, and we have good values for those
            // quantities.
            // 
            // If _FileDataPosition is -1, then we are consing up this Entry from scratch.  We
            // zero those quantities now, and we will compute actual values for the three
            // quantities later, when we do the compression, and then seek back to write them
            // into the appropriate place in the header.
            if (this.__FileDataPosition == -1)
            {
                _UncompressedSize = 0;
                _CompressedSize = 0;
                _Crc32 = 0;
                _crcCalculated = false;
            }

            // set compression method here
            FigureCompressionMethodForWriting(cycle);

            // (i==8) compression method         
            bytes[i++] = (byte)(CompressionMethod & 0x00FF);
            bytes[i++] = (byte)((CompressionMethod & 0xFF00) >> 8);

#if AESCRYPTO
            if (Encryption == EncryptionAlgorithm.WinZipAes128 || Encryption == EncryptionAlgorithm.WinZipAes256)
            {
                i -= 2;
                bytes[i++] = 0x63;
                bytes[i++] = 0;
            }
#endif

            // LastMod
            _TimeBlob = SharedUtilities.DateTimeToPacked(LastModified);

            // (i==10) time blob
            bytes[i++] = (byte)(_TimeBlob & 0x000000FF);
            bytes[i++] = (byte)((_TimeBlob & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((_TimeBlob & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((_TimeBlob & 0xFF000000) >> 24);

            // (i==14) CRC - if source==filesystem, this is zero now, actual value will be calculated later.
            // if source==archive, this is a bonafide value.
            bytes[i++] = (byte)(_Crc32 & 0x000000FF);
            bytes[i++] = (byte)((_Crc32 & 0x0000FF00) >> 8);
            bytes[i++] = (byte)((_Crc32 & 0x00FF0000) >> 16);
            bytes[i++] = (byte)((_Crc32 & 0xFF000000) >> 24);

            if (_presumeZip64)
            {
                // (i==18) CompressedSize (Int32) and UncompressedSize - all 0xFF for now
                for (j = 0; j < 8; j++)
                    bytes[i++] = 0xFF;
            }
            else
            {
                // (i==18) CompressedSize (Int32) - this value may or may not be bonafide.
                // if source == filesystem, then it is zero, and we'll learn it after we compress.
                // if source == archive, then it is bonafide data.
                bytes[i++] = (byte)(_CompressedSize & 0x000000FF);
                bytes[i++] = (byte)((_CompressedSize & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((_CompressedSize & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((_CompressedSize & 0xFF000000) >> 24);

                // (i==22) UncompressedSize (Int32) - this value may or may not be bonafide.
                bytes[i++] = (byte)(_UncompressedSize & 0x000000FF);
                bytes[i++] = (byte)((_UncompressedSize & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((_UncompressedSize & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((_UncompressedSize & 0xFF000000) >> 24);
            }

            // (i==26) filename length (Int16)
            bytes[i++] = (byte)(filenameLength & 0x00FF);
            bytes[i++] = (byte)((filenameLength & 0xFF00) >> 8);

            _Extra = ConsExtraField(false);

            // (i==28) extra field length (short)
            Int16 ExtraFieldLength = (Int16)((_Extra == null) ? 0 : _Extra.Length);
            bytes[i++] = (byte)(ExtraFieldLength & 0x00FF);
            bytes[i++] = (byte)((ExtraFieldLength & 0xFF00) >> 8);

            // The filename written to the archive.
            // The buffer is already encoded; we just copy across the bytes.
            for (j = 0; (j < FileNameBytes.Length) && (i + j < bytes.Length); j++)
                bytes[i + j] = FileNameBytes[j];

            i += j;

            // "Extra field"
            if (_Extra != null)
            {
                for (j = 0; j < _Extra.Length; j++)
                    bytes[i + j] = _Extra[j];
                i += j;
            }

            _LengthOfHeader = i;

            // finally, write the header to the stream
            s.Write(bytes, 0, i);

            // preserve this header data, we'll use it again later.
            // ..when seeking backward, to write again, after we have the Crc, compressed
            //   and uncompressed sizes.  
            // ..and when writing the central directory structure.
            _EntryHeader = new byte[i];
            for (j = 0; j < i; j++)
                _EntryHeader[j] = bytes[j];
        }




        private Int32 FigureCrc32()
        {
            if (_crcCalculated == false)
            {
                Stream input = null;
                // get the original stream:
                if (this._Source == ZipEntrySource.Stream)
                {
                    PrepSourceStream();
                    input = _sourceStream;
                }
                else
                {
                    //input = File.OpenRead(LocalFileName);
                    input = File.Open(LocalFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                var crc32 = new CRC32();

                _Crc32 = crc32.GetCrc32(input);
                if (_sourceStream == null)
                {
                    input.Close();
#if !NETCF20
                    input.Dispose();
#endif
                }
                _crcCalculated = true;
            }
            return _Crc32;
        }

        /// <summary>
        /// Stores the position of the entry source stream, or, if the position is
        /// already stored, seeks to that position.
        /// </summary>
        ///
        /// <remarks>
        /// This method is called in prep for reading the source stream.  If PKZIP
        /// encryption is used, then we need to calc the CRC32 before doing the
        /// encryption.  Hence we need to be able to seek backward in the source when
        /// saving the ZipEntry. This method is called from the place which calculates
        /// the CRC, and also from the method that does the encryption of the file data.
        /// </remarks>
        private void PrepSourceStream()
        {
            if (_sourceStream == null)
                throw new ZipException(String.Format("The input stream is null for entry '{0}'.", FileName));

            if (this._sourceStreamOriginalPosition != null)
            {
                // this will happen the 2nd cycle through, if the stream is seekable
                this._sourceStream.Position = this._sourceStreamOriginalPosition.Value;
            }
            else if (this._sourceStream.CanSeek)
            {
                // this will happen the first cycle through, if seekable
                this._sourceStreamOriginalPosition = new Nullable<Int64>(this._sourceStream.Position);
            }
            else if (this.Encryption == EncryptionAlgorithm.PkzipWeak)
                throw new ZipException("It is not possible to use PKZIP encryption on a non-seekable stream");
        }


        /// <summary>
        /// Copy metadata that may have been changed by the app.  We do this when
        /// resetting the zipFile instance.  If the app calls Save() on a ZipFile, then
        /// tries to party on that file some more, we may need to Reset() it , which
        /// means re-reading the entries and then copying the metadata.  I think.
        /// </summary>
        internal void CopyMetaData(ZipEntry source)
        {
            this.__FileDataPosition = source.__FileDataPosition;
            this.CompressionMethod = source.CompressionMethod;
            this._CompressedFileDataSize = source._CompressedFileDataSize;
            this._UncompressedSize = source._UncompressedSize;
            this._BitField = source._BitField;
            this._Source = source._Source;
            this._LastModified = source._LastModified;
            this._Mtime = source._Mtime;
            this._Atime = source._Atime;
            this._Ctime = source._Ctime;
            this._ntfsTimesAreSet = source._ntfsTimesAreSet;
            this._emitUnixTimes = source._emitUnixTimes;
            this._emitNtfsTimes = source._emitNtfsTimes;
        }


        private void _WriteFileData(Stream s)
        {
            // Read in the data from the input stream (often a file in the filesystem),
            // and write it to the output stream, calculating a CRC on it as we go.
            // We will also deflate and encrypt as necessary. 

            Stream input = null;
            CrcCalculatorStream input1 = null;
            CountingStream outputCounter = null;
            try
            {
                // s.Position may fail on some write-only streams, eg stdout or
                // System.Web.HttpResponseStream We swallow that exception, because we don't
                // care!
                this.__FileDataPosition = s.Position;
            }
            catch { }

            try
            {
                long fileLength = 0;
                // get the original stream:
                if (this._Source == ZipEntrySource.Stream)
                {
                    PrepSourceStream();
                    input = this._sourceStream;

                    //if (this._sourceStream.CanSeek)
                    // Try to get the length, no big deal if not available.
                    try { fileLength = this._sourceStream.Length; }
                    catch (NotSupportedException) { }
                }
                else
                {
                    //input = File.OpenRead(LocalFileName);
                    // workitem 7145
                    FileShare fs = FileShare.ReadWrite;
#if !NETCF
                    // FileShare.Delete is not defined for the Compact Framework
                    fs |= FileShare.Delete;
#endif
                    FileInfo fi = new FileInfo(LocalFileName);
                    fileLength = fi.Length;

                    input = File.Open(LocalFileName, FileMode.Open, FileAccess.Read, fs);
                }


                // wrap a CRC Calculator Stream around the raw input stream. 
                input1 = new CrcCalculatorStream(input);

                // Wrap a counting stream around the raw output stream:
                // This is the last thing that happens before the bits go to the 
                // application-provided stream. 
                outputCounter = new CountingStream(s);

                // Maybe wrap an encrypting stream around that:
                // This will happen AFTER deflation but before counting, if encryption 
                // is used.
                Stream output1 = outputCounter;
                if (Encryption == EncryptionAlgorithm.PkzipWeak)
                    output1 = new ZipCipherStream(outputCounter, _zipCrypto, CryptoMode.Encrypt);

#if AESCRYPTO

                else if (Encryption == EncryptionAlgorithm.WinZipAes128 ||
                         Encryption == EncryptionAlgorithm.WinZipAes256)
                {
                    output1 = new WinZipAesCipherStream(outputCounter, _aesCrypto, CryptoMode.Encrypt);
                }
#endif


                //Stream output1a = new TraceStream(output1);

                // Maybe wrap a DeflateStream around that.
                // This will happen BEFORE encryption (if any) as we write data out.
                Stream output2 = null;
                bool mustCloseDeflateStream = false;
                if (CompressionMethod == 0x08)
                {
                    var o = new DeflateStream(output1, CompressionMode.Compress,
                                                         _zipfile.CompressionLevel,
                                                         true);
                    if (_zipfile.CodecBufferSize > 0)
                        o.BufferSize = _zipfile.CodecBufferSize;
                    o.Strategy = _zipfile.Strategy;
                    mustCloseDeflateStream = true;
                    output2 = o;
                }
                else
                {
                    output2 = output1;
                }

                //Stream output2 = new TraceStream(output2a);

                // as we emit the file, we maybe deflate, then maybe encrypt, then write the bytes. 
                byte[] buffer = new byte[BufferSize];
                int n;
                while ((n = SharedUtilities.ReadWithRetry(input1, buffer, 0, buffer.Length, FileName)) != 0)
                {
                    output2.Write(buffer, 0, n);
                    OnWriteBlock(input1.TotalBytesSlurped, fileLength);
                    if (_ioOperationCanceled)
                        break;
                }

                // by calling Close() on the deflate stream, we write the footer bytes, as necessary.
                if (mustCloseDeflateStream)
                    output2.Close();

                output1.Flush();
                output1.Close();

                _LengthOfTrailer = 0;
#if AESCRYPTO
                WinZipAesCipherStream wzacs = output1 as WinZipAesCipherStream;
                if (wzacs != null)
                {
                    s.Write(wzacs.FinalAuthentication, 0, 10);
                    _LengthOfTrailer += 10;
                }
#endif
            }
            finally
            {

                if (this._Source != ZipEntrySource.Stream && input != null)
                {
                    input.Close();
#if !NETCF
                    input.Dispose();
#endif
                }
            }

            if (_ioOperationCanceled)
                return;

            _UncompressedSize = input1.TotalBytesSlurped;
            _CompressedFileDataSize = outputCounter.BytesWritten;
            _CompressedSize = _CompressedFileDataSize; // may be adjusted
            _Crc32 = input1.Crc;

            if (_Password != null)
            {
                if (Encryption == EncryptionAlgorithm.PkzipWeak)
                {
                    _CompressedSize += 12; // 12 extra bytes for the encryption header
                }
#if AESCRYPTO
                else if (Encryption == EncryptionAlgorithm.WinZipAes128 ||
                         Encryption == EncryptionAlgorithm.WinZipAes256)
                {
                    // adjust the compressed size to include the variable (salt+pv) 
                    // security header and 10-byte trailer. according to the winzip AES
                    // spec, that metadata is included in the "Compressed Size" figure
                    // when encoding the zip archive.
                    _CompressedSize += _aesCrypto.SizeOfEncryptionMetadata;
                }
#endif
            }


            int i = 8;
            _EntryHeader[i++] = (byte)(CompressionMethod & 0x00FF);
            _EntryHeader[i++] = (byte)((CompressionMethod & 0xFF00) >> 8);

            i = 14;
            // CRC - the correct value now
            _EntryHeader[i++] = (byte)(_Crc32 & 0x000000FF);
            _EntryHeader[i++] = (byte)((_Crc32 & 0x0000FF00) >> 8);
            _EntryHeader[i++] = (byte)((_Crc32 & 0x00FF0000) >> 16);
            _EntryHeader[i++] = (byte)((_Crc32 & 0xFF000000) >> 24);

            // zip64 housekeeping
            _entryRequiresZip64 = new Nullable<bool>
                (_CompressedSize >= 0xFFFFFFFF || _UncompressedSize >= 0xFFFFFFFF || _RelativeOffsetOfLocalHeader >= 0xFFFFFFFF);

            // validate the ZIP64 usage
            if (_zipfile._zip64 == Zip64Option.Never && _entryRequiresZip64.Value)
                throw new ZipException("Compressed or Uncompressed size, or offset exceeds the maximum value. Consider setting the UseZip64WhenSaving property on the ZipFile instance.");

            _OutputUsesZip64 = new Nullable<bool>(_zipfile._zip64 == Zip64Option.Always || _entryRequiresZip64.Value);

            // (i==26) filename length (Int16)
            Int16 filenameLength = (short)(_EntryHeader[26] + _EntryHeader[27] * 256);
            Int16 extraFieldLength = (short)(_EntryHeader[28] + _EntryHeader[29] * 256);

            if (_OutputUsesZip64.Value)
            {
                // VersionNeededToExtract - set to 45 to indicate zip64
                _EntryHeader[4] = (byte)(45 & 0x00FF);
                _EntryHeader[5] = 0x00;

                // workitem 7924 - don't need bit 3
                // // workitem 7917
                // // set bit 3 for ZIP64 compatibility with WinZip12
                // _BitField |= 0x0008;
                // _EntryHeader[6] = (byte)(_BitField & 0x00FF);

                // CompressedSize and UncompressedSize - 0xFF
                for (int j = 0; j < 8; j++)
                    _EntryHeader[i++] = 0xff;

                // At this point we need to find the "Extra field" that follows
                // the filename.  We had already emitted it, but the data
                // (uncomp, comp, Relative Offset) was not available at the
                // time we did so.  Here, we emit it again, with final values.

                i = 30 + filenameLength;
                _EntryHeader[i++] = 0x01;  // zip64
                _EntryHeader[i++] = 0x00;

                i += 2; // skip over data size, which is 16+4

                Array.Copy(BitConverter.GetBytes(_UncompressedSize), 0, _EntryHeader, i, 8);
                i += 8;
                Array.Copy(BitConverter.GetBytes(_CompressedSize), 0, _EntryHeader, i, 8);
            }
            else
            {
                // VersionNeededToExtract - reset to 20 since no zip64
                _EntryHeader[4] = (byte)(20 & 0x00FF);
                _EntryHeader[5] = 0x00;

                // CompressedSize - the correct value now
                i = 18;
                _EntryHeader[i++] = (byte)(_CompressedSize & 0x000000FF);
                _EntryHeader[i++] = (byte)((_CompressedSize & 0x0000FF00) >> 8);
                _EntryHeader[i++] = (byte)((_CompressedSize & 0x00FF0000) >> 16);
                _EntryHeader[i++] = (byte)((_CompressedSize & 0xFF000000) >> 24);

                // UncompressedSize - the correct value now
                _EntryHeader[i++] = (byte)(_UncompressedSize & 0x000000FF);
                _EntryHeader[i++] = (byte)((_UncompressedSize & 0x0000FF00) >> 8);
                _EntryHeader[i++] = (byte)((_UncompressedSize & 0x00FF0000) >> 16);
                _EntryHeader[i++] = (byte)((_UncompressedSize & 0xFF000000) >> 24);

                // The HeaderId in the extra field header, is already dummied out.
                if (extraFieldLength != 0)
                {
                    i = 30 + filenameLength;
                    // For zip archives written by this library, if the zip64 header exists, 
                    // it is the first header. Because of the logic used when first writing the 
                    // _EntryHeader bytes, the HeaderId is not guaranteed to be any
                    // particular value.  So we determine if the first header is a putative zip64
                    // header by examining the datasize.  
                    //UInt16 HeaderId = (UInt16)(_EntryHeader[i] + _EntryHeader[i + 1] * 256);
                    Int16 DataSize = (short)(_EntryHeader[i + 2] + _EntryHeader[i + 3] * 256);
                    if (DataSize == 16)
                    {
                        // reset to Header Id to dummy value, effectively dummy-ing out the zip64 metadata
                        _EntryHeader[i++] = 0x99;
                        _EntryHeader[i++] = 0x99;
                    }
                }
            }


#if AESCRYPTO

            if (Encryption == EncryptionAlgorithm.WinZipAes128 ||
                Encryption == EncryptionAlgorithm.WinZipAes256)
            {
                // Must set compressionmethod to 0x0063 (decimal 99)
                // and then set the compression method bytes inside the extra field to the actual 
                // compression method value.

                i = 8;
                _EntryHeader[i++] = 0x63;
                _EntryHeader[i++] = 0;

                i = 30 + filenameLength;
                do
                {
                    UInt16 HeaderId = (UInt16)(_EntryHeader[i] + _EntryHeader[i + 1] * 256);
                    Int16 DataSize = (short)(_EntryHeader[i + 2] + _EntryHeader[i + 3] * 256);
                    if (HeaderId != 0x9901)
                    {
                        // skip this header
                        i += DataSize + 4;
                    }
                    else
                    {
                        i += 9;
                        // actual compression method
                        _EntryHeader[i++] = (byte)(_CompressionMethod & 0x00FF);
                        _EntryHeader[i++] = (byte)(_CompressionMethod & 0xFF00);
                    }
                } while (i < (extraFieldLength - 30 - filenameLength));

            }
#endif


            // workitem 7216 - sometimes we don't seek even if we CAN.
            // ASP.NET Response.OutputStream, or stdout are non-seekable.
            // But we may also want to NOT seek in other cases, eg zip64.
            // For all cases, we just check bit 3 to see if we want to seek.

            if ((_BitField & 0x0008) != 0x0008)
            {
                // seek in the raw output stream, to the beginning of the header for
                // this entry.
                // workitem 8098: ok (output)
                s.Seek(this._RelativeOffsetOfLocalHeader, SeekOrigin.Begin);

                // write the updated header to the output stream
                s.Write(_EntryHeader, 0, _EntryHeader.Length);

                // adjust the count on the CountingStream as necessary
                var s1 = s as CountingStream;
                if (s1 != null) s1.Adjust(_EntryHeader.Length);

                // seek in the raw output stream, to the end of the file data for this entry
                s.Seek(_CompressedSize, SeekOrigin.Current);
            }
            else
            {
                byte[] Descriptor = new byte[16 + (_OutputUsesZip64.Value?8:0)];
                i = 0;
                    
                // signature
                Array.Copy(BitConverter.GetBytes(ZipConstants.ZipEntryDataDescriptorSignature), 0, Descriptor, i, 4);
                i += 4;

                // CRC - the correct value now
                Array.Copy(BitConverter.GetBytes(_Crc32), 0, Descriptor, i, 4);
                i += 4;

                // workitem 7917
                if (_OutputUsesZip64.Value)
                {
                    // CompressedSize - the correct value now
                    Array.Copy(BitConverter.GetBytes(_CompressedSize), 0, Descriptor, i, 8);
                    i += 8;

                    // UncompressedSize - the correct value now
                    Array.Copy(BitConverter.GetBytes(_UncompressedSize), 0, Descriptor, i, 8);
                    i += 8;
                }
                else
                {
                    // CompressedSize - (lower 32 bits) the correct value now
                    Descriptor[i++] = (byte)(_CompressedSize & 0x000000FF);
                    Descriptor[i++] = (byte)((_CompressedSize & 0x0000FF00) >> 8);
                    Descriptor[i++] = (byte)((_CompressedSize & 0x00FF0000) >> 16);
                    Descriptor[i++] = (byte)((_CompressedSize & 0xFF000000) >> 24);

                    // UncompressedSize - (lower 32 bits) the correct value now
                    Descriptor[i++] = (byte)(_UncompressedSize & 0x000000FF);
                    Descriptor[i++] = (byte)((_UncompressedSize & 0x0000FF00) >> 8);
                    Descriptor[i++] = (byte)((_UncompressedSize & 0x00FF0000) >> 16);
                    Descriptor[i++] = (byte)((_UncompressedSize & 0xFF000000) >> 24);
                }

                // finally, write the trailing descriptor to the output stream
                s.Write(Descriptor, 0, Descriptor.Length);

                _LengthOfTrailer += Descriptor.Length;
            }
        }



        private void OnZipErrorWhileSaving(Exception e)
        {
            _ioOperationCanceled = _zipfile.OnZipErrorSaving(this, e); 
        }



        internal void Write(Stream s)
        {
            bool done = false;
            do
            {
                if (_Source == ZipEntrySource.ZipFile && !_restreamRequiredOnSave)
                {
                    CopyThroughOneEntry(s);
                    return;
                }


                // Ok, the source for this entry is not a previously created zip file, or
                // the settings whave changed in important ways and therefore we will need to
                // process the bytestream (compute crc, maybe compress, maybe encrypt) in
                // order to create the zip.
                //
                // We do this in potentially 2 passes: The first time we do it as requested, maybe
                // with compression and maybe encryption.  If that causes the bytestream to inflate
                // in size, and if compression was on, then we turn off compression and do it again.

                try 
                {
                    if (IsDirectory)
                    {
                        WriteHeader(s, 1);
                        // nothing more to write
                        _entryRequiresZip64 = new Nullable<bool>(_RelativeOffsetOfLocalHeader >= 0xFFFFFFFF);
                        _OutputUsesZip64 = new Nullable<bool>(_zipfile._zip64 == Zip64Option.Always || _entryRequiresZip64.Value);
                        return;
                    }


                    bool readAgain = true;
                    int nCycles = 0;
                    do
                    {
                        nCycles++;

                        WriteHeader(s, nCycles);

                        // now, write the actual file data. (incl the encrypted header)
                        _EmitOne(s);

                        // The file data has now been written to the stream, and 
                        // the file pointer is positioned directly after file data.

                        if (nCycles > 1) readAgain = false;
                        else if (!s.CanSeek) readAgain = false;
                        else readAgain = WantReadAgain();

                        if (readAgain)
                        {
                            // Seek back in the raw output stream, to the beginning of the file
                            // data for this entry.

                            // workitem 8098: ok (output).
                            s.Seek(_RelativeOffsetOfLocalHeader, SeekOrigin.Begin);

                            // If the last entry expands, we read again; but here, we must
                            // truncate the stream to prevent garbage data after the
                            // end-of-central-directory.

                            // workitem 8098: ok (output).
                            s.SetLength(s.Position);

                            // Adjust the count on the CountingStream as necessary.
                            var s1 = s as CountingStream;
                            if (s1 != null) s1.Adjust(_TotalEntrySize);
                        }
                    } 
                    while (readAgain);
                    _skippedDuringSave= false;
                    done = true;
                }
                catch (System.Exception exc1)
                {
                    ZipErrorAction orig = this.ZipErrorAction;
                    int loop = 0;
                    do
                    {
                        if (ZipErrorAction == ZipErrorAction.Throw)
                            throw;
                
                        if (ZipErrorAction == ZipErrorAction.Skip ||
                            ZipErrorAction == ZipErrorAction.Retry)
                        {
                            // must reset file pointer here.
                            if (!s.CanSeek) throw;
                            long p1 = s.Position;
                            s.Seek(_RelativeOffsetOfLocalHeader, SeekOrigin.Begin);
                            long p2 = s.Position;
                            s.SetLength(s.Position);  // to prevent garbage if this is the last entry
                            var s1 = s as CountingStream;
                            if (s1 != null) s1.Adjust(p1-p2);
                            if (ZipErrorAction == ZipErrorAction.Skip)
                            {
                                if (_zipfile.StatusMessageTextWriter!= null)
                                    _zipfile.StatusMessageTextWriter.WriteLine("Skipping file {0} (exception: {1})", LocalFileName, exc1.ToString());
                                
                                _skippedDuringSave= true;
                                done = true;
                            }
                            else
                                this.ZipErrorAction = orig;
                            break;
                        }

                        if (loop>0) throw;
                    
                        if (ZipErrorAction == ZipErrorAction.InvokeErrorEvent)
                        {
                            OnZipErrorWhileSaving(exc1);
                            if (_ioOperationCanceled)
                            {
                                done = true;
                                break;
                            }
                        }
                        loop++;
                    }
                    while (true);
                } 
            } 
            while (!done);
        }




        private void _EmitOne(Stream outstream)
        {
            _WriteSecurityMetadata(outstream);

            // write the (potentially compressed, potentially encrypted) file data
            _WriteFileData(outstream);

            // track total entry size (including the trailing descriptor and MAC)
            _TotalEntrySize = _LengthOfHeader + _CompressedFileDataSize + _LengthOfTrailer;
        }



        private void _WriteSecurityMetadata(Stream outstream)
        {
            //Console.WriteLine("_WriteSecurityMetadata({0})", FileName);
            if (_Password == null) return;
            if (Encryption == EncryptionAlgorithm.PkzipWeak)
            {
                // If PKZip (weak) encryption is in use, then the encrypted entry data
                // is preceded by 12-byte "encryption header" for the entry.

                _zipCrypto = ZipCrypto.ForWrite(_Password);

                // generate the random 12-byte header:
                var rnd = new System.Random();
                byte[] encryptionHeader = new byte[12];
                rnd.NextBytes(encryptionHeader);

                // workitem 8271
                if ((this._BitField & 0x0008) == 0x0008)
                {
                    // In the case that bit 3 of the general purpose bit flag is set to
                    // indicate the presence of a 'data
                    // descriptor' (signature 0x08074b50), the last byte of the decrypted
                    // header is sometimes compared with the high-order byte of the
                    // lastmodified time, rather than the high-order byte of the CRC, to
                    // verify the password.
                    //
                    // This is not documented in the PKWare Appnote.txt.  
                    // This was discovered this by analysis of the Crypt.c source file in the InfoZip library
                    // http://www.info-zip.org/pub/infozip/

                    // Also, winzip insists on this!
                    _TimeBlob = SharedUtilities.DateTimeToPacked(LastModified);
                    encryptionHeader[11] = (byte)((this._TimeBlob >> 8) & 0xff);
                }
                else
                {
                    // When bit 3 is not set, the CRC value is required before
                    // encryption of the file data begins. In this case there is no way
                    // around it: must read the stream in its entirety to compute the
                    // actual CRC before proceeding.
                    FigureCrc32();
                    encryptionHeader[11] = (byte)((this._Crc32 >> 24) & 0xff);
                }

                // Encrypt the random header, INCLUDING the final byte which is either
                // the high-order byte of the CRC32, or the high-order byte of the
                // _TimeBlob.  Must do this BEFORE encrypting the file data.  This
                // step changes the state of the cipher, or in the words of the PKZIP
                // spec, it "further initializes" the cipher keys.
                
                byte[] cipherText = _zipCrypto.EncryptMessage(encryptionHeader, encryptionHeader.Length);

                // Write the ciphered bonafide encryption header. 
                outstream.Write(cipherText, 0, cipherText.Length);
                _LengthOfHeader+= cipherText.Length;
            }

#if AESCRYPTO
            else if (Encryption == EncryptionAlgorithm.WinZipAes128 ||
                Encryption == EncryptionAlgorithm.WinZipAes256)
            {
                // If WinZip AES encryption is in use, then the encrypted entry data is
                // preceded by a variable-sized Salt and a 2-byte "password
                // verification" value for the entry.

                _aesCrypto = WinZipAesCrypto.Generate(_Password, _KeyStrengthInBits);
                outstream.Write(_aesCrypto.Salt, 0, _aesCrypto._Salt.Length);
                outstream.Write(_aesCrypto.GeneratedPV, 0, _aesCrypto.GeneratedPV.Length);
                _LengthOfHeader+= _aesCrypto._Salt.Length + _aesCrypto.GeneratedPV.Length;
    }
#endif

        }


        private void CopyThroughOneEntry(Stream outstream)
        {
            // Just read the entry from the existing input zipfile and write to the output.
            // But, if metadata has changed (like file times or attributes), or if the ZIP64
            // option has changed, we can re-stream the entry data but must recompute the 
            // metadata. 
            if (this.LengthOfHeader == 0)
                    throw new BadStateException("Bad header length.");

            var input1 = new CrcCalculatorStream(this.ArchiveStream);

            // is it necessaty to re-streammetadata for this entry? 
            bool needRecompute = _metadataChanged ||
                (_InputUsesZip64 && _zipfile.UseZip64WhenSaving == Zip64Option.Never) ||
                (!_InputUsesZip64 && _zipfile.UseZip64WhenSaving == Zip64Option.Always);
            
            if (needRecompute)
                CopyThroughWithRecompute(outstream, input1);
            else
                CopyThroughWithNoChange(outstream, input1);

            // zip64 housekeeping
            _entryRequiresZip64 = new Nullable<bool>
                (_CompressedSize >= 0xFFFFFFFF || _UncompressedSize >= 0xFFFFFFFF ||
                _RelativeOffsetOfLocalHeader >= 0xFFFFFFFF
                );

            _OutputUsesZip64 = new Nullable<bool>(_zipfile._zip64 == Zip64Option.Always || _entryRequiresZip64.Value);

        }

        private void CopyThroughWithRecompute(Stream outstream, CrcCalculatorStream input1)
        {
            int n;
            byte[] bytes = new byte[BufferSize];
            Stream input = this.ArchiveStream;

            long origRelativeOffsetOfHeader = _RelativeOffsetOfLocalHeader;

            // The header length may change due to rename of file, add a comment, etc.
            // We need to retain the original. 
            int origLengthOfHeader = LengthOfHeader; // including crypto bytes!

            // WriteHeader() has the side effect of changing _RelativeOffsetOfLocalHeader 
            // and setting _LengthOfHeader.  While ReadHeader() reads the crypto header if
            // present, WriteHeader() does not write the crypto header.
            WriteHeader(outstream, 0);

            if (!this.FileName.EndsWith("/"))
            {
                // not a directory, we have file data
                // seek to the beginning of the entry data in the input stream
                long pos = origRelativeOffsetOfHeader + origLengthOfHeader;
                pos -= LengthOfCryptoHeaderBytes; // want to keep the crypto header
                _LengthOfHeader += LengthOfCryptoHeaderBytes;

                // change for workitem 8098
                //input.Seek(pos, SeekOrigin.Begin);
                this._zipfile.SeekFromOrigin(pos);

                // copy through everything after the header to the output stream
                long remaining = this._CompressedSize;

                while (remaining > 0)
                {
                    int len = (remaining > bytes.Length) ? bytes.Length : (int)remaining;

                    // read
                    n = input1.Read(bytes, 0, len);
                    //_CheckRead(n);

                    // write
                    outstream.Write(bytes, 0, n);
                    remaining -= n;
                    OnWriteBlock(input1.TotalBytesSlurped, this._CompressedSize);
                    if (_ioOperationCanceled)
                        break;
                }

                // bit 3 descriptor
                if ((this._BitField & 0x0008) == 0x0008)
                {
                    int size = 16;
                    if (_InputUsesZip64) size += 8;
                    byte[] Descriptor = new byte[size];
                    input.Read(Descriptor, 0, size);

                    if (_InputUsesZip64 && _zipfile.UseZip64WhenSaving == Zip64Option.Never)
                    {
                        // original descriptor was 24 bytes, now we need 16.
                        // Must check for underflow here.
                        // signature + CRC.
                        outstream.Write(Descriptor, 0, 8);

                        // Compressed
                        if (_CompressedSize > 0xFFFFFFFF)
                            throw new InvalidOperationException("ZIP64 is required");
                        outstream.Write(Descriptor, 8, 4);

                        // UnCompressed
                        if (_UncompressedSize > 0xFFFFFFFF)
                            throw new InvalidOperationException("ZIP64 is required");
                        outstream.Write(Descriptor, 16, 4);
                        _LengthOfTrailer -= 8;
                    }
                    else if (!_InputUsesZip64 && _zipfile.UseZip64WhenSaving == Zip64Option.Always)
                    {
                        // original descriptor was 16 bytes, now we need 24
                        // signature + CRC
                        byte[] pad = new byte[4];
                        outstream.Write(Descriptor, 0, 8);
                        // Compressed
                        outstream.Write(Descriptor, 8, 4);
                        outstream.Write(pad, 0, 4);
                        // UnCompressed
                        outstream.Write(Descriptor, 12, 4);
                        outstream.Write(pad, 0, 4);
                        _LengthOfTrailer += 8;
                    }
                    else
                    {
                        // same descriptor on input and output. Copy it through.
                        outstream.Write(Descriptor, 0, size);
                        //_LengthOfTrailer += size;
                    }
                }
            }

            _TotalEntrySize = _LengthOfHeader + _CompressedFileDataSize + _LengthOfTrailer;
        }

        private void CopyThroughWithNoChange(Stream outstream, CrcCalculatorStream input1)
        {
            int n;
            byte[] bytes = new byte[BufferSize];

            //long origRelativeOffsetOfHeader = _RelativeOffsetOfLocalHeader;

            // seek to the beginning of the entry data (header + file data) in the input stream
            //input.Seek(this._RelativeOffsetOfLocalHeader, SeekOrigin.Begin);

            // Here, we need to grab the header and fill it with real data. Some of
            // the fields may take marker values - eg, the CRC may be all zero and
            // the Uncomp and Comp sizes may be 0xFFFFFFFF.  Those are all "fake"
            // values, but we need to set the real ones into the header.  We don't
            // write the header here; instead we're just copying through.  But the
            // _EntryHeader array is used later when writing the Central Directory
            // Structure, and the header data must be correct at that point.

            // ?? that is a doomed approach !! 

            //_EntryHeader = new byte[this._LengthOfHeader];
            //n = input.Read(_EntryHeader, 0, _EntryHeader.Length);
            //_CheckRead(n);

            // once again, seek to the beginning of the entry data in the input stream
            // change for workitem 8098
            //input.Seek(this._RelativeOffsetOfLocalHeader, SeekOrigin.Begin);
            this._zipfile.SeekFromOrigin(this._RelativeOffsetOfLocalHeader);

            if (this._TotalEntrySize == 0)
            {
                // We've never set the length of the entry.  
                // Set it here.
                this._TotalEntrySize = this._LengthOfHeader + this._CompressedFileDataSize + _LengthOfTrailer;

                // The CompressedSize includes all the leading metadata associated
                // to encryption, if any, as well as the compressed data, or
                // compressed-then-encrypted data, and the trailer in case of AES.

                // The CompressedFileData size is the same, less the encryption
                // framing data (12 bytes header for PKZip; 10/18 bytes header and
                // 10 byte trailer for AES).

                // The _LengthOfHeader includes all the zip entry header plus the
                // crypto header, if any.  The _LengthOfTrailer includes the
                // 10-byte MAC for AES, where appropriate, and the bit-3
                // Descriptor, where applicable.
            }


            // workitem 5616
            // remember the offset, within the output stream, of this particular entry header.
            // This may have changed if any of the other entries changed (eg, if a different
            // entry was removed or added.)
            var counter = outstream as CountingStream;
            _RelativeOffsetOfLocalHeader = (counter != null) ? counter.BytesWritten : outstream.Position;

            // copy through the header, filedata, trailer, everything...
            long remaining = this._TotalEntrySize;
            while (remaining > 0)
            {
                int len = (remaining > bytes.Length) ? bytes.Length : (int)remaining;

                // read
                n = input1.Read(bytes, 0, len);
                //_CheckRead(n);

                // write
                outstream.Write(bytes, 0, n);
                remaining -= n;
                //OnWriteBlock(input1.TotalBytesSlurped, this._CompressedSize);
                OnWriteBlock(input1.TotalBytesSlurped, this._TotalEntrySize);
                if (_ioOperationCanceled)
                    break;
            }
        }


       
    }
}

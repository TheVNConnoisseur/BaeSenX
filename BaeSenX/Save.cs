using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BaeSenX
{
    internal class Save
    {
        byte[] CompiledSave { get; set; } = Array.Empty<byte>();
        string Checksum { get; set; } = string.Empty;

        public Save(byte[] OriginalFile)
        {
            CompiledSave = OriginalFile;
        }

        public byte[] GetCompiledSave()
        {
            return CompiledSave;
        }

        /// <summary>
        /// The first 32 bytes of the save represent the MD5 checksum of the opcode region
        /// of its corresponding compiled script file. Said checksum is used by the game
        /// to determine if its compatible, and if not, it automatically deletes it.
        /// </summary>
        public void SetChecksum()
        {
            Checksum = Encoding.UTF8.GetString(CompiledSave, 0, 32);
        }

        /// <summary>
        /// To calculate the updated checksum, we need to get the opcode region of the script
        /// file. Once the array is obtained, we compute its MD5 hash and convert it to lowercase.
        /// If the hash is different from the one currently stored in the save, then the CompiledSave
        /// will be updated with the new checksum.
        /// </summary>
        public void SetUpdatedChecksum(byte[][] CompiledScriptOpcodeArrays)
        {
            //The GetRawList function returns two arrays, the metadata and the actual contents.
            //The checksum function only takes into consideration the contents, since the 
            //opcode region does not contain any metadata.
            byte[] CompiledScriptOpcodeArray = CompiledScriptOpcodeArrays[1];

            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(CompiledScriptOpcodeArray);
            string NewChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            if (NewChecksum != Checksum)
            {
                Checksum = NewChecksum;
                byte[] ChecksumArray = Encoding.UTF8.GetBytes(Checksum);
                Buffer.BlockCopy(ChecksumArray, 0, CompiledSave, 0, 32);
            }
        }


    }
}

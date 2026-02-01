using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows.Shapes;

namespace BaeSenX
{
    class BSXScript
    {
        float Version { get; set; } = new();
        byte[] CompiledScript { get; set; } = Array.Empty<byte>();

        public class FunctionArray
        {
            public string Name { get; set; }
            public int Address { get; set; }
        }
        
        public FunctionArray[] FunctionList { get; set; } = Array.Empty<FunctionArray>();
        public List<string[]> VariableList { get; } = new();
        public string[] CharacterList { get; set; } = Array.Empty<string>();
        public string[] MessageList { get; set; } = Array.Empty<string>();

        public (int OffsetMetadata, int SizeMetadata, int OffsetList, int SizeList, int BytesToShift)[] HeaderParameters = Array.Empty<(int OffsetMetadata, int SizeMetadata, int OffsetList, int SizeList, int BytesToShift)>();

        List<Instruction> Instructions = new List<Instruction>();
        
        public BSXScript(byte[] OriginalFile)
        {
            CompiledScript = OriginalFile;
            byte[] MagicSignature = new byte[16]; //While the magic signature is technically 13 bytes, the game reserves 16 bytes for it for alignment purposes
            Buffer.BlockCopy(CompiledScript, 0, MagicSignature, 0, 16);

            Version = GetVersion(MagicSignature);

            //Depending on the version, some things may vary, although at this moment that's not the case
            switch (Version)
            {
                case 3.0f:
                case 3.1f:
                case 3.2f:
                case 3.3f:
                    for (int CurrentArray = 0; CurrentArray < 4; CurrentArray++) //There are 4 lists of generic variables that the game uses
                        VariableList.Add(Array.Empty<string>());

                    HeaderParameters =  //The header does include the offsets and sizes of each of the lists for the script file, with the bytes to shift being a hardcoded value inside the game's executable
                    [
                        (0, 0, 0x2C, 0x30, 0),        //Opcodes list
                        (0x38, 0x3C, 0x40, 0x44, 3),  //Functions list
                        (0x48, 0x4C, 0x50, 0x54, 2),  //1nd variable list
                        (0x58, 0x5C, 0x60, 0x64, 2),  //2rd variable list
                        (0x68, 0x6C, 0x70, 0x74, 2),  //3th variable list
                        (0x78, 0x7C, 0x80, 0x84, 2),  //4th variable list
                        (0x88, 0x8C, 0x90, 0x94, 2),  //Characters list
                        (0x98, 0x9C, 0xA0, 0xA4, 2)   //Messages list
                    ];
                    break;
            }
        }

        /// <summary>
        /// Takes a byte array representing the header of a BSXScript file and returns the version number.
        /// Said header is expected to be 16 bytes long and contain the version information in ASCII format.
        /// </summary>
        /// <returns>Float with version number</returns>
        public static float GetVersion(byte[] MagicSignature)
        {
            if (MagicSignature.SequenceEqual(Encoding.ASCII.GetBytes("BSXScript 3.0\x00\x00\x00")))
                return 3.0f;
            if (MagicSignature.SequenceEqual(Encoding.ASCII.GetBytes("BSXScript 3.1\x00\x00\x00")))
                return 3.1f;
            if (MagicSignature.SequenceEqual(Encoding.ASCII.GetBytes("BSXScript 3.2\x00\x00\x00")))
                return 3.2f;
            if (MagicSignature.SequenceEqual(Encoding.ASCII.GetBytes("BSXScript 3.3\x00\x00\x00")))
                return 3.3f;

            throw new ArgumentException("Malformated header, no valid version has been detected.");
        }

        /// <summary>
        /// Obtains from the main array of the binary script file the raw data of a specific list,
        /// both its metadata and its content.
        /// </summary>
        /// <returns>Byte arrray of both metadata and actual data for the asked list.</returns>
        public byte[][] GetRawList(int ListNumber)
        {
            //First we obtain the parameters for the list we want to extract, which are located at the header of the file
            var (MetadataOffset, MetadataSize, ListOffset,
                ListSize, _) = HeaderParameters[ListNumber];

            //The first list is for the metadata of said list, since it's own separate list
            byte[] MetadataList = new byte[BitConverter.ToInt32(CompiledScript, MetadataSize)];

            //Some lists don't have metadata, so we skip the copying if that's the case
            if (MetadataSize != 0)
            {
                Buffer.BlockCopy(
                    CompiledScript,
                    BitConverter.ToInt32(CompiledScript, MetadataOffset),
                    MetadataList,
                    0,
                    MetadataList.Length);
            }

            //Now we obtain the list with the actual data
            byte[] ContentList = new byte[BitConverter.ToInt32(CompiledScript, ListSize)];
            Buffer.BlockCopy(
                CompiledScript, 
                BitConverter.ToInt32(CompiledScript, ListOffset),
                ContentList, 
                0, 
                ContentList.Length);

            byte[][] List = [
                MetadataList, ContentList
                ];

            return List;
        }


        /// <summary>
        /// Converts the compiled script into a list of instructions that can be more easily analyzed.
        /// </summary>
        /// <returns>List of Instructions that is based on the opcodes section of the script, filled
        /// with the metadata obtained from the other sections.</returns>
        public List<Instruction> Decompile()
        {
            //First we obtain the opcodes array
            byte[] OpcodesArray = GetRawList(0)[1];

            //The next section is the one with the functions/labels
            byte[] MetadataFunctionsArray = GetRawList(1)[0];
            byte[] FunctionsArray = GetRawList(1)[1];
            var (_, _, _, _, FunctionsMetadataBytesToShift) = HeaderParameters[1];

            FunctionList = DecompilerHelper.GetFunctions(MetadataFunctionsArray, 
                FunctionsArray, FunctionsMetadataBytesToShift);


            //Now we obtain the variables from each of the lists with variables
            for (int CurrentList = 0; CurrentList < VariableList.Count; CurrentList++)
            {
                byte[] MetadataVariablesArray = GetRawList(CurrentList + 2)[0];
                byte[] VariablesArray = GetRawList(CurrentList + 2)[1];
                var (_, _, _, _, VariablesMetadataBytesToShift) = HeaderParameters[CurrentList + 2];

                VariableList[CurrentList] = DecompilerHelper.GetListVariables(MetadataVariablesArray, 
                    VariablesArray, VariablesMetadataBytesToShift);
            }


            //Next, we have the character's names
            byte[] MetadataCharactersArray = GetRawList(HeaderParameters.Length - 2)[0];
            byte[] CharactersArray = GetRawList(HeaderParameters.Length - 2)[1];
            var (_, _, _, _, CharactersMetadataBytesToShift) = HeaderParameters[HeaderParameters.Length - 2];

            CharacterList = DecompilerHelper.GetListVariables(MetadataCharactersArray, 
                CharactersArray, CharactersMetadataBytesToShift);

            //The final list contains the messages all of the characters say during the game
            byte[] MetadataMessagesArray = GetRawList(HeaderParameters.Length - 1)[0];
            byte[] MessagesArray = GetRawList(HeaderParameters.Length - 1)[1];
            var (_, _, _, _, MessagesMetadataBytesToShift) = HeaderParameters[HeaderParameters.Length - 1];

            MessageList = DecompilerHelper.GetListVariables(MetadataMessagesArray,
                MessagesArray, MessagesMetadataBytesToShift);

            return AnalyzeOpcodes(OpcodesArray);
        }

        public List<Instruction> AnalyzeOpcodes(byte[] Data)
        {
            int CurrentOffset = 0;

            while (CurrentOffset < Data.Length)
            {
                Instruction Instruction = new Instruction();
                Instruction.Type = Data[CurrentOffset].ToString("X2") + " " + (CurrentOffset + 256);
                //Instruction.Type = Data[CurrentOffset].ToString("X2");

                switch (Data[CurrentOffset])
                {
                    //This effectively just acts like the opcode 0x38, but the label is always "boot", basically resetting
                    //the entire game
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x00:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Returns always 1, possibly as a NOP opcode, that way the engine doesn't do anything new nor different
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x01:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Returns always 2, possibly as a NOP opcode, that way the engine doesn't do anything new nor different
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x02:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Jumps to a specific label, which is defined in the script
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: label ID
                    case 0x03:
                        {
                            int LabelID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(FunctionList[LabelID].Name);

                            CurrentOffset += 5;
                            break;
                        }

                    //Seems to be similar to 0x03, a jump to a specific address, although not exactly the same way
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: address to jump to
                    case 0x04:
                        {
                            Instruction.Arguments.Add(BitConverter.ToInt32(Data, CurrentOffset + 1).ToString());
                            CurrentOffset += 5;
                            break;
                        }

                    //Jumps to a specific address. But if the address is a null one, it will not jump, and instead
                    //continue to the next instruction.
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: address to jump to
                    case 0x05:
                        {
                            int Address = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(Address.ToString());

                            CurrentOffset += 5;
                            break;
                        }

                    //This one is a bit unclear, it seems to be a fake jump, which is used to go to a specific label.
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: label ID
                    case 0x06:
                        {
                            int LabelID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(LabelID.ToString());

                            CurrentOffset += 5;
                            break;
                        }

                    //Jumps to a specific label, which is defined in the script
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: label ID
                    case 0x07:
                    case 0x08:
                        {
                            int LabelID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(FunctionList[LabelID].Name);

                            CurrentOffset += 5;
                            break;
                        }

                    //Calls a function by its name, which is defined in the function list.
                    //This opcode indicates that the script should jump to where the opcode 0x38 is located with the same
                    //function name as here.
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: index for the function in the function list
                    case 0x09:
                        {
                            int FunctionID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(FunctionList[FunctionID].Name);
                            CurrentOffset += 5;
                            break;
                        }

                    //It indicates the end of a label, and the script will go back to it when it is called
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x0A:
                        {
                            CurrentOffset += 1;
                            break;
                        }
                    case 0x0B: // reset stack pointer
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Unknown opcode, it seems to be used for calculations.
                    //Structure to follow:
                    //1 byte: opcode. This is what dictates what operation to perform.
                    //1 byte: unused
                    //4 bytes: unknown, it seems to be where to store the value of the result
                    //2 bytes: source of the first variable
                    //4 bytes: index of the first variable to use
                    //2 bytes: source of the second variable
                    //4 bytes: index of the second variable to use
                    case 0x0C:
                    case 0x0D:
                    case 0x0E:
                    case 0x0F:
                    case 0x10:
                    case 0x11:
                    case 0x13:
                    case 0x14:
                    case 0x15:
                    case 0x16:
                    case 0x17:
                    case 0x18:
                    case 0x19:
                    case 0x3A:
                    case 0x3B:
                    case 0x3C:
                        {
                            int Destination = BitConverter.ToInt32(Data, CurrentOffset + 2);
                            Instruction.Arguments.Add(Destination.ToString());

                            ushort SourceFirstValue = BitConverter.ToUInt16(Data, CurrentOffset + 6);
                            Instruction.Arguments.Add(SourceFirstValue.ToString());

                            if (SourceFirstValue >= 256 && 262 <= SourceFirstValue)
                            {
                                throw new Exception("The source of the parameters of this opcode must be between 256 and 262.");
                            }

                            int FirstValue = BitConverter.ToInt32(Data, CurrentOffset + 8);
                            Instruction.Arguments.Add(VariableList[2][FirstValue]);

                            switch (Data[CurrentOffset])
                            {
                                case 0x0C:
                                    Instruction.Arguments.Add(">");
                                    break;
                                case 0x0D:
                                    Instruction.Arguments.Add("<");
                                    break;
                                case 0x0E:
                                    Instruction.Arguments.Add(">=");
                                    break;
                                case 0x0F:
                                    Instruction.Arguments.Add("<=");
                                    break;
                                case 0x10:
                                    Instruction.Arguments.Add("!=");
                                    break;
                                case 0x11:
                                    Instruction.Arguments.Add("==");
                                    break;
                                case 0x13:
                                    Instruction.Arguments.Add("|");
                                    break;
                                case 0x14:
                                    Instruction.Arguments.Add("&");
                                    break;
                                case 0x15:
                                    Instruction.Arguments.Add("+");
                                    break;
                                case 0x16:
                                    Instruction.Arguments.Add("-");
                                    break;
                                case 0x17:
                                    Instruction.Arguments.Add("*");
                                    break;
                                case 0x18:
                                    Instruction.Arguments.Add("/");
                                    break;
                                case 0x19:
                                    Instruction.Arguments.Add("%");
                                    break;
                                case 0x3A:
                                    Instruction.Arguments.Add("^");
                                    break;
                                case 0x3B:
                                    Instruction.Arguments.Add("<<");
                                    break;
                                case 0x3C:
                                    Instruction.Arguments.Add(">>");
                                    break;
                            }

                            ushort SourceSecondValue = BitConverter.ToUInt16(Data, CurrentOffset + 12);
                            Instruction.Arguments.Add(SourceSecondValue.ToString());

                            if (SourceSecondValue >= 256 && 262 <= SourceSecondValue)
                            {
                                throw new Exception("The source of the parameters of this opcode must be between 256 and 262.");
                            }

                            int SecondValue = BitConverter.ToInt32(Data, CurrentOffset + 14);
                            Instruction.Arguments.Add(SecondValue.ToString());

                            CurrentOffset += 18;
                            break;
                        }

                    //Load an EV image (even faces) or a sound effect (those can be a character making a sound)
                    //Structure to follow:
                    //1 byte: opcode
                    //5 bytes: null bytes for padding
                    //2 bytes: unknown
                    //4 bytes: unknown
                    //2 bytes: unknown
                    //4 bytes: index of the file to load in the 1st variable list
                    case 0x12:
                        {
                            ushort FirstValue = BitConverter.ToUInt16(Data, CurrentOffset + 6);
                            uint SecondValue = BitConverter.ToUInt32(Data, CurrentOffset + 8);
                            ushort ThirdValue = BitConverter.ToUInt16(Data, CurrentOffset + 12);
                            int FourthValue = BitConverter.ToInt32(Data, CurrentOffset + 14);

                            switch(FirstValue)
                            {
                                case 258:
                                case 260:
                                case 261:
                                case 262:
                                    Instruction.Arguments.Add(FirstValue.ToString());
                                    Instruction.Arguments.Add(SecondValue.ToString());
                                    Instruction.Arguments.Add(ThirdValue.ToString());
                                    if (FourthValue < 0) //To revise in which case this causes problems (only seen once with case 260)
                                    {
                                        Instruction.Arguments.Add(FourthValue.ToString());
                                    }
                                    else
                                        Instruction.Arguments.Add(VariableList[0][FourthValue]);
                                    break;
                                case 259:   //Modify a variable case
                                    Instruction.Arguments.Add("Modify variable");
                                    Instruction.Arguments.Add(VariableList[2][SecondValue]);  //The variable to modify
                                    Instruction.Arguments.Add(ThirdValue.ToString());  //To be revised
                                    Instruction.Arguments.Add(FourthValue.ToString());
                                    break;
                                default:
                                    throw new Exception("The first parameter of this opcode must be between 258 and 262.");
                            }

                            //To put accordingly inside the switch statement
                            if (ThirdValue >= 256 && 262 <= ThirdValue)
                            {
                                throw new Exception("The third parameter of this opcode must be between 258 and 262.");
                            }

                            CurrentOffset += 18;
                            break;
                        }
                    
                    //Never seen it, so its utility is yet to be confirmed. It seems to be some kind of negated statement of sorts.
                    //Structure to follow:
                    //1 byte: opcode
                    //5 bytes: null bytes for padding
                    //2 bytes: type of negated statement
                    //4 bytes: index of variable to use (the list to use it not known)
                    case 0x1A:
                        {
                            ushort FirstValue = BitConverter.ToUInt16(Data, CurrentOffset + 6);
                            Instruction.Arguments.Add(FirstValue.ToString());

                            if (FirstValue >= 256 && 262 <= FirstValue)
                            {
                                throw new Exception("The first parameter of this opcode must be between 258 and 262.");
                            }

                            uint SecondValue = BitConverter.ToUInt32(Data, CurrentOffset + 8);
                            Instruction.Arguments.Add(SecondValue.ToString());

                            CurrentOffset += 12;
                            break;
                        }

                    //Its functionality is yet to be understood. Apparently the only difference between 0x1B and 0x1C is that
                    //the first one is used for adding 1 to a slot, and 0x1C is used for subtracting 1 from a slot
                    //Structure to follow:
                    //1 byte: opcode
                    //5 bytes: null bytes for padding
                    //2 bytes: unknown, maybe it refers to the source of the variable
                    //4 bytes: index of the variable to use
                    case 0x1B:
                    case 0x1C:
                        {
                            ushort FirstValue = BitConverter.ToUInt16(Data, CurrentOffset + 6);
                            Instruction.Arguments.Add(FirstValue.ToString());

                            if (FirstValue >= 258 && 262 <= FirstValue)
                            {
                                throw new Exception("The first parameter of this opcode must be between 258 and 262.");
                            }

                            uint SecondValue = BitConverter.ToUInt32(Data, CurrentOffset + 8);
                            Instruction.Arguments.Add(SecondValue.ToString());

                            CurrentOffset += 12;
                            break;
                        }

                    //Loads a message (and if it is voiced, its corresponding voice file)
                    //Structure to follow:
                    //1 byte: opcode
                    //1 byte: type of message
                    //4 bytes: the index of the message in the messages list
                    //4 bytes: the index of the character in the characters list
                    //4 bytes: how many sounds it does include (it only supports from 1 to 2)
                    //4 * (Number of files) bytes: the index of the sound file in the 1st variables list
                    case 0x1D:
                        {
                            switch (Data[CurrentOffset + 1])
                            {
                                //The message has no character associated to it
                                case 0:
                                    {
                                        int CurrentMessage = BitConverter.ToInt32(Data, CurrentOffset + 2);
                                        Instruction.Arguments.Add(MessageList[CurrentMessage]);
                                        
                                        CurrentOffset += 6;
                                        break;
                                    }

                                //The message has a character associated to it, but no sound file associated with it
                                case 1:
                                    {
                                        int CurrentMessage = BitConverter.ToInt32(Data, CurrentOffset + 2);
                                        int CharacterMessage = BitConverter.ToInt32(Data, CurrentOffset + 6);
                                        Instruction.Arguments.Add(MessageList[CurrentMessage]);
                                        Instruction.Arguments.Add(CharacterList[CharacterMessage]);

                                        CurrentOffset += 10;
                                        break;
                                    }
                                //The message has a character associated to it, and a sound file for the dialogue
                                case 2:
                                case 3:
                                    {
                                        int CurrentMessage = BitConverter.ToInt32(Data, CurrentOffset + 2);
                                        int CharacterMessage = BitConverter.ToInt32(Data, CurrentOffset + 6);
                                        Instruction.Arguments.Add(MessageList[CurrentMessage]);
                                        Instruction.Arguments.Add(CharacterList[CharacterMessage]);
                                        int NumberofFiles = BitConverter.ToInt32(Data, CurrentOffset + 10);
                                        Instruction.Arguments.Add(NumberofFiles.ToString());
                                        
                                        for (int CurrentFile = 0; CurrentFile < NumberofFiles; CurrentFile++)
                                        {
                                            int SoundMessage = BitConverter.ToInt32(Data, CurrentOffset + 14 + 4 * CurrentFile);
                                            Instruction.Arguments.Add(VariableList[0][SoundMessage]);
                                        }

                                        CurrentOffset += 14 + 4 * NumberofFiles;
                                        break;
                                    }
                                default:
                                    {
                                        throw new Exception("Unknown message type.");
                                    }
                            }

                            break;
                        }

                    //It seems that at least 0x27 is used to signify the end of a label
                    case 0x1E:
                    case 0x1F:
                    case 0x20:
                    case 0x21:
                    case 0x22:
                    case 0x23:
                    case 0x24:
                    case 0x25:
                    case 0x26:
                    case 0x27:
                    case 0x28:
                    case 0x29:
                    case 0x2A:
                    case 0x2B:
                    case 0x2C:
                    case 0x2D:
                    case 0x2E:
                    case 0x2F:
                    case 0x30:
                    case 0x31:
                    case 0x32:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Option for creating a selecting an option during a scene (a branching path)
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: the point of teleportation ID to jump into when this option gets selected (0x38 instruction)
                    //4 bytes: the number of the option in the list of options for that particular branching path
                    //4 bytes: the index of the message in the messages list to show inside the option label
                    case 0x33:
                        {
                            int TeleportationID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(TeleportationID.ToString());

                            int OptionNumber = BitConverter.ToInt32(Data, CurrentOffset + 5);
                            Instruction.Arguments.Add(OptionNumber.ToString());

                            int MessageID = BitConverter.ToInt32(Data, CurrentOffset + 9);
                            Instruction.Arguments.Add(MessageList[MessageID]);

                            CurrentOffset += 13;
                            break;
                        }
                    
                    //Opcode solely used for indicating the end of all options to select in a branching path, which corresponds
                    //to the opcode 0x33
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: null bytes for padding (always set to FF)
                    case 0x34:
                        {
                            CurrentOffset += 5;
                            break;
                        }
                    case 0x35:
                        {
                            CurrentOffset += 1;
                            break;
                        }
                    case 0x36:
                        {
                            int FunctionID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            if (FunctionID < 0)
                            {
                                Instruction.Arguments.Add(FunctionID.ToString());
                            }
                            else
                            {
                                Instruction.Arguments.Add(FunctionList[FunctionID].Name);
                            }

                            CurrentOffset += 5;
                            break;
                        }
                    case 0x37:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Point of teleportation in the script (the game calls it functions). When another opcodes calls a specific
                    //label, the script will jump to this point and continue executing from there.
                    //This opcode indicates where the script should go to when the function is called through opcode 0x09.
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: ID of the point of teleportation (said number increments by 1 per each 0x38 instruction)
                    case 0x38:
                        {
                            int LabelID = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(FunctionList[LabelID].Name);
                            CurrentOffset += 5;
                            break;
                        }
                    case 0x39:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Unknown opcode, since it does not show up at all. It seems to perform a bitwise NOT operations
                    //on values specified here and stores them in a variable slots table
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: index of the variable to use
                    //2 bytes: variable table that comes from the variable
                    //4 bytes: unknown
                    case 0x3D:
                        {
                            int FirstValue = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(FirstValue.ToString());

                            ushort SecondValue = BitConverter.ToUInt16(Data, CurrentOffset + 5);
                            Instruction.Arguments.Add(SecondValue.ToString());

                            if (SecondValue >= 256 && SecondValue <= 262)
                            {
                                throw new Exception("The second parameter of this opcode must be between 256 and 262.");
                            }

                            int ThirdValue = BitConverter.ToInt32(Data, CurrentOffset + 7);
                            Instruction.Arguments.Add(ThirdValue.ToString());

                            CurrentOffset += 12;
                            break;
                        }

                    //Opcode yet to be understood.
                    //Structure to follow:
                    //1 byte: opcode
                    //4 bytes: amount of repetitions
                    //4 * (Number of repetitions) bytes: unknown
                    case 0x3E:
                        {
                            int NumberOfRepetitions = BitConverter.ToInt32(Data, CurrentOffset + 1);
                            Instruction.Arguments.Add(NumberOfRepetitions.ToString());


                            for (int CurrentRepetition = 0; CurrentRepetition < NumberOfRepetitions; CurrentRepetition++)
                            {
                                int CurrentValue = BitConverter.ToInt32(Data, CurrentOffset + 5 + 4 * CurrentRepetition);
                                Instruction.Arguments.Add(CurrentRepetition.ToString());
                            }
                            CurrentOffset += 5 + 4 * BitConverter.ToInt32(Data, CurrentOffset + 1);
                            break;
                        }

                    //Never seen being used, so its utility is yet to be confirmed. Although it seems to be some kind of
                    //opcode used for some debugging purposes.
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x3F:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //Never seen being used, so its utility is yet to be confirmed.
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x40:
                        {
                            CurrentOffset += 1;
                            break;
                        }

                    //NOP opcode, so it basically does nothing.
                    //Structure to follow:
                    //1 byte: opcode
                    case 0x41:
                        {
                            CurrentOffset += 1;
                            break;
                        }
                    default:
                        {
                            throw new Exception("Invalid opcode found at byte " + CurrentOffset + ". Remember that this offset " +
                                "is relative to the opcode array, so if you wanna find it in the actual script, you first must remove " +
                                "the entire header.");
                        }
                }
                
                Instructions.Add(Instruction);
            }

            return Instructions;
        }

        public byte[] Recompile(string JSON)
        {
            //First we need to deserialize the JSON string into a list of instructions
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            Instructions = JsonSerializer.Deserialize<List<Instruction>>(JSON, options) ?? new List<Instruction>();

            List<byte> CompiledScript = new List<byte>();



            return CompiledScript.ToArray();
        }
    }
}

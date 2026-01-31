using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace BaeSenX
{
    internal class DecompilerHelper
    {
        public static string[] GetListVariables(byte[] MetadataArray, byte[] VariablesArray,
            int BytesToShift)
        {
            int NumberOfVariables = MetadataArray.Length >> BytesToShift;  //This is determined by the game's executable itself

            string[] VariableList = new string[NumberOfVariables];
            int[] VariableOffset = new int[NumberOfVariables]; //This array will contain the offset of all of the variables

            int CurrentOffset = 0;

            //First we will obtain the metadata for each of the functions, which is the offset for the name of said variable
            //inside the actual array of variable names.
            //It is very important to note that these offsets aren't actually offsets per se, but rather, it tells us how many
            //double-byte characters one has to cross in order to reach said variable name.
            for (int CurrentVariable = 0; CurrentVariable < NumberOfVariables; CurrentVariable++)
            {
                VariableOffset[CurrentVariable] = BitConverter.ToInt32(MetadataArray, CurrentOffset) * 2;
                CurrentOffset += 4;
            }

            CurrentOffset = 0;

            //Now with the offsets, it is possible to obtain the actual variable names. The last two bytes for each name
            //are always a null double-byte character, so we can safely ignore it when checking the actual name.
            for (int CurrentVariable = 0; CurrentVariable < NumberOfVariables; CurrentVariable++)
            {
                int VariableLength = 0;
                if (CurrentVariable == NumberOfVariables - 1)
                {
                    VariableLength = VariablesArray.Length - VariableOffset[CurrentVariable];
                }
                else
                {
                    VariableLength = VariableOffset[CurrentVariable + 1] - CurrentOffset;
                }

                VariableList[CurrentVariable] = Encoding.Unicode.GetString(VariablesArray,
                    VariableOffset[CurrentVariable], VariableLength - 2);
                CurrentOffset += VariableLength;
            }

            return VariableList;
        }

        /// <summary>
        /// Function that generates an array of the functions the game can run at any time. Said functions are
        /// stored inside the first list of the script.
        /// </summary>
        /// <returns>Always a FunctionArray</returns>
        public static BSXScript.FunctionArray[] GetFunctions(byte[] MetadataArray, byte[] FunctionsArray,
            int BytesToShift)
        {
            int NumberOfFunctions = MetadataArray.Length >> BytesToShift;  //This is determined by the game's executable itself
            BSXScript.FunctionArray[] FunctionList = new BSXScript.FunctionArray[NumberOfFunctions];

            int[] FunctionOffset = new int[NumberOfFunctions]; //This array will contain the offset of all of the functions,
                                                               //will help us in obtaining all of the function's names

            int CurrentOffset = 0;

            //First we will obtain the metadata for each of the functions, which in this list is the address (while not confirmed,
            //it seems to be the actual address inside the game's executable that contains the code that will be executed in order
            //to load the desired function), and then comes the offset for the name of said function inside the actual array of function
            //names.
            //It is very important to note that these offsets aren't actually offsets per se, but rather, it tells us how many
            //double-byte characters one has to cross in order to reach said function name.
            for (int CurrentFunction = 0; CurrentFunction < NumberOfFunctions; CurrentFunction++)
            {
                FunctionList[CurrentFunction] = new BSXScript.FunctionArray();
                FunctionList[CurrentFunction].Address = BitConverter.ToInt32(MetadataArray, CurrentOffset);
                FunctionOffset[CurrentFunction] = BitConverter.ToInt32(MetadataArray, CurrentOffset + 4) * 2;
                CurrentOffset += 8;
            }

            CurrentOffset = 0;

            //Now with the offsets, it is possible to obtain the actual function names. The last two bytes for each name
            //are always a null double-byte character, so we can safely ignore it when checking the actual name.
            for (int CurrentFunction = 0; CurrentFunction < NumberOfFunctions; CurrentFunction++)
            {
                int FunctionLength = 0;
                if (CurrentFunction == NumberOfFunctions - 1)
                {
                    FunctionLength = FunctionsArray.Length - FunctionOffset[CurrentFunction];
                }
                else
                {
                    FunctionLength = FunctionOffset[CurrentFunction + 1] - CurrentOffset;
                }

                FunctionList[CurrentFunction].Name = Encoding.Unicode.GetString(FunctionsArray, 
                    FunctionOffset[CurrentFunction], FunctionLength - 2);
                CurrentOffset += FunctionLength;
            }

            return FunctionList;
        }
    }
}

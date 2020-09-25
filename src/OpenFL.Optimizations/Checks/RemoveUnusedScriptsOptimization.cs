using System.Collections.Generic;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;

using Utility.ADL;

namespace OpenFL.Optimizations.Checks
{
    public class RemoveUnusedScriptsOptimization : FLProgramCheck<SerializableFLProgram>
    {

        public override int Priority => 3;

        public override FLProgramCheckType CheckType => FLProgramCheckType.Optimization;

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram) o;
            Dictionary<string, bool> scripts = new Dictionary<string, bool>();
            input.ExternalFunctions.ForEach(x => scripts.Add(x.Name, false));

            foreach (SerializableFLFunction serializableFlFunction in input.Functions)
            {
                foreach (SerializableFLInstruction serializableFlInstruction in serializableFlFunction.Instructions)
                {
                    foreach (SerializableFLInstructionArgument serializableFlInstructionArgument in
                        serializableFlInstruction.Arguments)
                    {
                        switch (serializableFlInstructionArgument.ArgumentCategory)
                        {
                            case InstructionArgumentCategory.Script:
                                scripts[serializableFlInstructionArgument.Identifier] = true;
                                break;
                        }
                    }
                }
            }


            foreach (KeyValuePair<string, bool> keyValuePair in scripts)
            {
                if (keyValuePair.Value)
                {
                    continue; //Function used. Dont Remove
                }

                for (int i = input.ExternalFunctions.Count - 1; i >= 0; i--)
                {
                    if (input.ExternalFunctions[i].Name == keyValuePair.Key)
                    {
                        Logger.Log(LogType.Log, "Removing Script: " + keyValuePair.Key, 1);
                        input.ExternalFunctions.RemoveAt(i);
                    }
                }
            }

            foreach (SerializableExternalFLFunction serializableFlFunction in input.ExternalFunctions)
            {
                Process(serializableFlFunction.ExternalProgram);
            }

            return input;
        }

    }
}
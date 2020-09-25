using System.Collections.Generic;

using OpenFL.Core;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.Exceptions;
using OpenFL.Core.ProgramChecks;

using Utility.ADL;

namespace OpenFL.Optimizations.Checks
{
    public class RemoveUnusedBuffersOptimization : FLProgramCheck<SerializableFLProgram>
    {

        public override int Priority => 3;

        public override FLProgramCheckType CheckType => FLProgramCheckType.Optimization;

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram) o;
            Dictionary<string, bool> buffers = new Dictionary<string, bool>();
            input.DefinedBuffers.ForEach(x => buffers.Add(x.Name, x.Name == FLKeywords.InputBufferKey));


            foreach (SerializableFLFunction serializableFlFunction in input.Functions)
            {
                foreach (SerializableFLInstruction serializableFlInstruction in serializableFlFunction.Instructions)
                {
                    foreach (SerializableFLInstructionArgument serializableFlInstructionArgument in
                        serializableFlInstruction.Arguments)
                    {
                        if ((serializableFlInstructionArgument.ArgumentCategory &
                             InstructionArgumentCategory.AnyBuffer) !=
                            0)
                        {
                            buffers[serializableFlInstructionArgument.Identifier] = true;
                        }
                        else if (serializableFlInstructionArgument.Identifier.StartsWith("~"))
                        {
                            string bufferName = serializableFlInstructionArgument.Identifier.Remove(0, 1);
                            if (!buffers.ContainsKey(bufferName))
                            {
                                throw new FLProgramCheckException(
                                                                  "Possible wrong variable name: " +
                                                                  bufferName +
                                                                  " in Instruction: " +
                                                                  serializableFlFunction,
                                                                  this
                                                                 );
                            }

                            buffers[bufferName] = true;
                        }
                    }
                }
            }

            Logger.Log(LogType.Log, "Removing Buffers", 1);
            foreach (KeyValuePair<string, bool> keyValuePair in buffers)
            {
                if (keyValuePair.Value)
                {
                    continue; //Function used. Dont Remove
                }

                for (int i = input.DefinedBuffers.Count - 1; i >= 0; i--)
                {
                    if (input.DefinedBuffers[i].Name == keyValuePair.Key)
                    {
                        input.DefinedBuffers.RemoveAt(i);
                    }
                }
            }

            foreach (SerializableExternalFLFunction serializableFlFunction in input.ExternalFunctions
            ) //Process all Subsequent scripts
            {
                Process(serializableFlFunction.ExternalProgram);
            }

            return input;
        }

    }
}
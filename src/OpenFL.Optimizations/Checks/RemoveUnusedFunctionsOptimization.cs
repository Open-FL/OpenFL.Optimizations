using System.Collections.Generic;

using OpenFL.Core;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;

using Utility.ADL;

namespace OpenFL.Optimizations.Checks
{
    public class RemoveUnusedFunctionsOptimization : FLProgramCheck<SerializableFLProgram>
    {

        public override int Priority => 3;

        public override FLProgramCheckType CheckType => FLProgramCheckType.Optimization;

        private Dictionary<string, bool> ParseFunctions(SerializableFLProgram input)
        {
            Dictionary<string, bool> funcs = new Dictionary<string, bool>();
            input.Functions.ForEach(x => funcs.Add(x.Name, x.Name == FLKeywords.EntryFunctionKey));
            Logger.Log(LogType.Log, "Finding Unused Functions.", 2);

            foreach (SerializableFLFunction serializableFlFunction in input.Functions)
            {
                foreach (SerializableFLInstruction instructionLine in serializableFlFunction.Instructions)
                {
                    foreach (SerializableFLInstructionArgument instructionArgument in instructionLine.Arguments)
                    {
                        if (funcs.ContainsKey(instructionArgument.Identifier))
                        {
                            funcs[instructionArgument.Identifier] = true;
                        }
                    }
                }
            }

            return funcs;
        }

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram) o;
            bool stop = false;
            int pass = 1;
            int removed = 0;

            while (!stop)
            {
                Logger.Log(LogType.Log, $"Pass {pass}: Generating Usage Info", 2);
                Dictionary<string, bool> funcs = ParseFunctions(input);
                bool removedOne = false;

                Logger.Log(LogType.Log, $"Pass {pass}: Applying Usage Info", 2);

                for (int i = input.Functions.Count - 1; i >= 0; i--)
                {
                    if (!funcs[input.Functions[i].Name])
                    {
                        removed++;
                        input.Functions.RemoveAt(i);
                        removedOne = true;
                    }
                }

                stop = !removedOne;
                pass++;
            }

            Logger.Log(LogType.Log, $"Removed {removed} Functions in {pass} passes.", 1);
            return input;
        }

    }
}
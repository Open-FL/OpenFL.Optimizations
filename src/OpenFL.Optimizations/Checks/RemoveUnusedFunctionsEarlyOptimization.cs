using System.Collections.Generic;

using OpenFL.Core;
using OpenFL.Core.Parsing.StageResults;
using OpenFL.Core.ProgramChecks;

using Utility.ADL;

namespace OpenFL.Optimizations.Checks
{
    public class RemoveUnusedFunctionsEarlyOptimization : FLProgramCheck<StaticInspectionResult>
    {

        public override int Priority => -1;

        public override FLProgramCheckType CheckType => FLProgramCheckType.Optimization;

        private Dictionary<string, bool> ParseFunctions(StaticInspectionResult input)
        {
            Dictionary<string, bool> funcs = new Dictionary<string, bool>();
            input.Functions.ForEach(x => funcs.Add(x.Name, x.Name == FLKeywords.EntryFunctionKey));
            Logger.Log(LogType.Log, "Finding Unused Functions.", 2);

            foreach (StaticFunction serializableFlFunction in input.Functions)
            {
                foreach (StaticInstruction instructionLine in serializableFlFunction.Body)
                {
                    foreach (string instructionArgument in instructionLine.Arguments)
                    {
                        if (funcs.ContainsKey(instructionArgument))
                        {
                            funcs[instructionArgument] = true;
                        }
                    }
                }
            }

            return funcs;
        }

        public override object Process(object o)
        {
            StaticInspectionResult input = (StaticInspectionResult) o;
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
using System.Collections.Generic;
using System.Linq;

using OpenFL.Core;
using OpenFL.Core.Arguments;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ElementModifiers;
using OpenFL.Core.Parsing.StageResults;
using OpenFL.Core.ProgramChecks;

namespace OpenFL.Optimizations.Checks
{
    public class StaticFLFunctionToExternalOptimization : FLProgramCheck<SerializableFLProgram>
    {

        public override FLProgramCheckType CheckType => FLProgramCheckType.AggressiveOptimization;


        public override int Priority => 3;

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram) o;
            Dictionary<string, SerializableFLFunction> staticFunctions =
                new Dictionary<string, SerializableFLFunction>();
            for (int i = input.Functions.Count - 1; i >= 0; i--)
            {
                SerializableFLFunction staticFunction = input.Functions[i];
                if (!staticFunction.Modifiers.IsStatic || staticFunction.Name == FLKeywords.EntryFunctionKey)
                {
                    continue;
                }

                string funcName = "_ext_" + staticFunction.Name;
                staticFunctions.Add(funcName, staticFunction);
                input.Functions.RemoveAt(i);

                List<string> source = staticFunction.Instructions.Select(x => x.ToString()).ToList();
                source.Insert(0, FLKeywords.EntryFunctionKey + ":");
                SerializableFLProgram ext = (SerializableFLProgram) Target.Process(
                     new FLParserInput(
                                       "Exported Function: " +
                                       staticFunction
                                           .Name,
                                       source.ToArray(),
                                       false
                                      )
                    );


                List<string> mods = new List<string> { FLKeywords.NoJumpKeyword };

                if (staticFunction.Modifiers.ComputeOnce)
                {
                    mods.Add(FLKeywords.ComputeOnceKeyword);
                }


                FLExecutableElementModifiers e = new FLExecutableElementModifiers(funcName, mods.ToArray());
                input.ExternalFunctions.Add(new SerializableExternalFLFunction(funcName, ext, e));
            }

            foreach (KeyValuePair<string, SerializableFLFunction> staticFunction in staticFunctions)
            {
                foreach (SerializableFLFunction serializableFlFunction in input.Functions)
                {
                    foreach (SerializableFLInstruction serializableFlInstruction in serializableFlFunction.Instructions)
                    {
                        for (int i = 0; i < serializableFlInstruction.Arguments.Count; i++)
                        {
                            SerializableFLInstructionArgument arg = serializableFlInstruction.Arguments[i];
                            if (arg.Identifier == staticFunction.Value.Name)
                            {
                                serializableFlInstruction.Arguments[i] =
                                    new SerializeExternalFunctionArgument(staticFunction.Key);
                            }
                        }
                    }
                }
            }

            return input;
        }

    }
}
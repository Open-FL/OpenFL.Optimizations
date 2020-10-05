using System.Collections.Generic;
using System.Linq;

using OpenFL.Core.Arguments;
using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;

namespace OpenFL.Optimizations.Checks
{
    public class OptimizeBufferCreation : FLProgramCheck<SerializableFLProgram>
    {

        public override int Priority => 3;

        public override FLProgramCheckType CheckType => FLProgramCheckType.Optimization;

        private string[] InstructionBlacklist = new[] { "jmp", "ble", "bge", "blt", "bgt" };

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram)o;

            input.ToString();

            List<SerializableFLFunction> optimizableFunctions = new List<SerializableFLFunction>();

            foreach (SerializableFLFunction function in input.Functions)
            {
                bool isUsed = false;
                foreach (SerializableFLFunction func in input.Functions)
                {
                    foreach (SerializableFLInstruction instruction in func.Instructions)
                    {
                        if(InstructionBlacklist.Contains(instruction.InstructionKey))
                        {
                            foreach (SerializableFLInstructionArgument argument in instruction.Arguments)
                            {
                                if (argument.Identifier == function.Name)
                                {
                                    isUsed = true;
                                    break;
                                }
                            }

                            if (isUsed) break;
                        }
                    }
                    if (isUsed) break;
                }
                if (!isUsed)
                {
                    optimizableFunctions.Add(function);
                }
            }
            //optimizableFunctions = input.Functions.Where(
            //                                             x => input.Functions.SelectMany(y => y.Instructions)
            //                                                       .All(
            //                                                            y => !InstructionBlacklist.Contains(
            //                                                                                                y.InstructionKey
            //                                                                                               ) &&
            //                                                                 y.Arguments.Any(
            //                                                                                 z => z.Identifier ==
            //                                                                                      x.Name
            //                                                                                )
            //                                                           )
            //                                            );



            foreach (SerializableFLFunction func in optimizableFunctions)
            {
                func.Instructions.Insert(
                                         0,
                                         new SerializableFLInstruction(
                                                                       "Set_v",
                                                                       new List<SerializableFLInstructionArgument
                                                                       > { new SerializeDecimalArgument(0) }
                                                                      )
                                        );
            }

            return input;
        }

    }
}
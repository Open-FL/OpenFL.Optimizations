using System;
using System.Collections.Generic;
using System.Linq;

using OpenCL.Wrapper;
using OpenCL.Wrapper.TypeEnums;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;

namespace OpenFL.Optimizations.Checks
{
    public class KernelMergeOptimization : FLProgramCheck<SerializableFLProgram>
    {

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram)o;

            IEnumerable<SerializableFLInstruction> targets = input.Functions.First(x => x.Name == "Main").Instructions.Take(2);

            string newName = "TESTKERNEL_01";
            string newSig = "";
            List<SerializableFLInstructionArgument> newArgs = new List<SerializableFLInstructionArgument>();
            List<string> source = new List<string>();
            int count = 0;
            foreach (SerializableFLInstruction serializableFlInstruction in targets)
            {
                newArgs.AddRange(serializableFlInstruction.Arguments);
                CLKernel instr = InstructionSet.Database.GetClKernel(serializableFlInstruction.InstructionKey);
                CLProgram prog = InstructionSet.Database.GetProgram(instr);
                source.Add(prog.Source);

                foreach (KeyValuePair<string, KernelParameter> kernelParameter in instr.Parameter.Skip(5))
                {
                    newSig += ", ";
                    switch (kernelParameter.Value.MemScope)
                    {
                        case MemoryScope.None:
                            break;
                        case MemoryScope.Global:
                            newSig += "__global ";
                            break;
                        case MemoryScope.Constant:
                            newSig += "__constant ";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    newSig += KernelParameter.GetDataString(kernelParameter.Value.DataType);
                    newSig += kernelParameter.Value.IsArray ? "* " : " ";
                    newSig += kernelParameter.Key + "_mrge_" + count;
                    count++;
                }
            }

            string sig = $"__kernel void {newName}(__global uchar* image, __global uchar* source, int channelCount{newSig})";

            Console.WriteLine(sig);
            Console.ReadLine();

            return input;
        }

        public override FLProgramCheckType CheckType => FLProgramCheckType.AggressiveOptimization;

    }
}
using System;
using System.Collections.Generic;
using System.Linq;

using OpenCL.Wrapper;
using OpenCL.Wrapper.TypeEnums;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;
using OpenFL.Parsing;

using Utility.ExtPP.API;
using Utility.ExtPP.Base;
using Utility.FastString;

namespace OpenFL.Optimizations.Checks
{
    public class KernelMergeOptimization : FLProgramCheck<SerializableFLProgram>
    {

        private string GetFunc(string name, string signature, string[] lines)
        {
            string kernelSig = $"void {name}({signature})\n";
            string kernelBlock = lines.Unpack("\n");
            return kernelSig + "{" + kernelBlock + "\n}";
        }

        private CLProgram Merge(CLProgram progA, CLProgram progB, string src)
        {
            string[] source = $"#includeinl {progA.FilePath}\n#includeinl {progB.FilePath}\n{src}".Split('\n');
            string content = TextProcessorAPI.PreprocessLines(source, "./", ".cl", new Dictionary<string, bool>()).Unpack("\n");
            CLProgramBuildResult res = CLProgram.TryBuildProgram(CLAPI.MainThread, content, "", out CLProgram newP);
            if (res)
            {
                return newP;
            }
            throw new Exception("RIP");
        }

        private string[] GetBlockContent(string source, string kernelName)
        {
            int kernelNameIndex = source.IndexOf(" " + kernelName + " (", StringComparison.InvariantCulture);
            kernelNameIndex = kernelNameIndex == -1
                                  ? source.IndexOf(" " + kernelName + "(", StringComparison.InvariantCulture)
                                  : kernelNameIndex;
            int start = source.IndexOf('{', kernelNameIndex) + 1;
            int current = 0;
            int end = -1;
            for (int i = start; i < source.Length; i++)
            {
                if (source[i] == '{') current++;
                if (source[i] == '}')
                {
                    if (current == 0)
                    {
                        end = i - 1;
                        break;
                    }
                    else
                    {
                        current--;
                    }
                }
            }

            return source.Substring(start, end - start).Pack("\n").ToArray();
        }

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram)o;
            
            IEnumerable<SerializableFLInstruction> targets = input.Functions.First(x => x.Name == "Main").Instructions.Take(2);

            string newName = "";
            string newSig = "";
            List<SerializableFLInstructionArgument> newArgs = new List<SerializableFLInstructionArgument>();
            List<string> lines = new List<string>();
            List<string> funcs = new List<string>();
            List<CLProgram> progs = new List<CLProgram>();
            int count = 0;
            foreach (SerializableFLInstruction serializableFlInstruction in targets)
            {
                newArgs.AddRange(serializableFlInstruction.Arguments);
                CLKernel instr = InstructionSet.Database.GetClKernel(serializableFlInstruction.InstructionKey);
                CLProgram prog = InstructionSet.Database.GetProgram(instr);
                progs.Add(prog);
                newName += "_" + instr.Name;
                string funcSig = "";
                List<(string orig, string newKey)> reps = new List<(string orig, string newKey)>();
                foreach (KeyValuePair<string, KernelParameter> kernelParameter in instr.Parameter.Skip(5))
                {
                    newSig += ", ";
                    switch (kernelParameter.Value.MemScope)
                    {
                        case MemoryScope.None:
                            break;
                        case MemoryScope.Global:
                            funcSig += "__global ";
                            break;
                        case MemoryScope.Constant:
                            funcSig += "__constant ";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    funcSig += KernelParameter.GetDataString(kernelParameter.Value.DataType);
                    funcSig += kernelParameter.Value.IsArray ? "* " : " ";
                    funcSig += kernelParameter.Key + "_mrge_" + count;
                    reps.Add((kernelParameter.Key, kernelParameter.Key + "_mrge_" + count));

                    count++;
                }
                newSig += funcSig;
                string[] block = GetBlockContent(prog.Source, instr.Name);
                foreach ((string orig, string newKey) valueTuple in reps)
                {
                    for (int i = 0; i < block.Length; i++)
                    {
                        block[i] = block[i].Replace(valueTuple.orig, valueTuple.newKey);
                    }
                }


                funcs.Add(GetFunc("gen_" + instr.Name, "__global uchar* image, int3 dimensions, int channelCount, float maxValue, __global uchar* channelEnableState, " + funcSig, block));
                string line = $"\ngen_{instr.Name}(image, dimensions, channelCount, maxValue, channelEnableState, {reps.Select(x => x.newKey).Unpack(", ")});\n";
                lines.Add(line);
            }

            string sig = $"__kernel void {newName}(__global uchar* image, int3 dimensions, int channelCount, float maxValue, __global uchar* channelEnableState{newSig})";
            string newk = $"{funcs.Unpack("\n\n")}\n{sig}\n" + "{" + lines.Unpack("\n") + "\n}";
            CLProgram newP = Merge(progs[0], progs[1], newk);
            Console.WriteLine(newk);
            Console.ReadLine();

            return input;
        }

        public override FLProgramCheckType CheckType => FLProgramCheckType.Disabled;

    }
}
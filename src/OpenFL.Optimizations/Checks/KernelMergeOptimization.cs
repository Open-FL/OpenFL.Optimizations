using System;
using System.Collections.Generic;
using System.Linq;

using OpenCL.Wrapper;
using OpenCL.Wrapper.TypeEnums;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;

using Utility.ExtPP.API;
using Utility.FastString;

namespace OpenFL.Optimizations.Checks
{
    public class KernelMergeOptimization : FLProgramCheck<SerializableFLProgram>
    {

        private static readonly char[] SpecialChars = new[] { ' ', ',' , '(', ')', '+', '-', '*', '/', ';', '^' };

        private static readonly string[] Blacklist =
        {
            "rnd_gpu",
            "urnd_gpu",
            "perlin",
            "worley",
            "smooth",
            "blur_x",
            "blur_y",
            "blur_z",
            "blur_xy",
            "blur_yz",
            "blur_xz",
            "blur_xyz"
        };

        public override int Priority => 3;

        public override FLProgramCheckType CheckType => FLProgramCheckType.AggressiveOptimization;

        private string GetFunc(string name, string signature, string[] lines)
        {
            string kernelSig = $"void {name}({signature})\n";
            string kernelBlock = lines.Unpack("\n");
            return kernelSig + "{" + kernelBlock + "\n}";
        }

        private string Merge(string src, params CLProgram[] progs)
        {
            string source = "";

            IEnumerable<CLProgram> unique = progs.Distinct(new ProgramComparer());

            foreach (CLProgram clProgram in unique)
            {
                source += $"#include {clProgram.FilePath}\n";
            }

            source += src;
            string[] lines = source.Split('\n');
            string content = TextProcessorAPI.PreprocessLines(lines, "./", ".cl", new Dictionary<string, bool>())
                                             .Unpack("\n");
            return content;
        }

        private bool CanBeOptimized(string name)
        {
            return !Blacklist.Contains(name) && InstructionSet.Database.KernelNames.Contains(name);
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
                if (source[i] == '{')
                {
                    current++;
                }

                if (source[i] == '}')
                {
                    if (current == 0)
                    {
                        end = i - 1;
                        break;
                    }

                    current--;
                }
            }

            return source.Substring(start, end - start).Pack("\n").ToArray();
        }

        private (string, string, CLProgram[]) GenerateTargets(SerializableFLInstruction[] targets)
        {
            string newName = "opt";
            string newSig = "";
            List<string> lines = new List<string>();
            Dictionary<string, string> funcs = new Dictionary<string, string>();
            List<CLProgram> progs = new List<CLProgram>();
            int count = 0;
            foreach (SerializableFLInstruction serializableFlInstruction in targets)
            {
                CLKernel instr = InstructionSet.Database.GetClKernel(serializableFlInstruction.InstructionKey);
                CLProgram prog = InstructionSet.Database.GetProgram(instr);
                progs.Add(prog);
                newName += "_" + instr.Name;
                string funcSig = "";
                List<(string orig, string newKey)> reps = new List<(string orig, string newKey)>();
                foreach (KeyValuePair<string, KernelParameter> kernelParameter in instr.Parameter.Skip(5))
                {
                    funcSig += ", ";
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
                        int current = block[i].IndexOf(valueTuple.orig, StringComparison.Ordinal);
                        while (current != -1)
                        {
                            if (CheckBack(block[i], current + valueTuple.orig.Length) &&
                                CheckFront(block[i], current))
                            {
                                block[i] = block[i].Remove(current, valueTuple.orig.Length).Insert(current, valueTuple.newKey);
                            }

                            current = block[i].IndexOf(valueTuple.orig, current + valueTuple.orig.Length, StringComparison.Ordinal);
                        }
                    }
                }

                string genFuncArgs = reps.Count == 0 ? "" : ", " + reps.Select(x => x.newKey).Unpack(", ");
                if (!funcs.ContainsKey(instr.Name))
                {
                    funcs.Add(
                              instr.Name,
                              GetFunc(
                                      "gen_" + instr.Name,
                                      "__global uchar* image, int3 dimensions, int channelCount, float maxValue, __global uchar* channelEnableState" +
                                      funcSig,
                                      block
                                     )
                             );
                }

                string line =
                    $"\ngen_{instr.Name}(image, dimensions, channelCount, maxValue, channelEnableState{genFuncArgs});\n";
                lines.Add(line);
            }

            string sig =
                $"__kernel void {newName}(__global uchar* image, int3 dimensions, int channelCount, float maxValue, __global uchar* channelEnableState{newSig})";
            string newk = $"{funcs.Values.Distinct().Unpack("\n\n")}\n{sig}\n" + "{" + lines.Unpack("\n") + "\n}";

            return (newName, newk, progs.ToArray());
        }

        private bool CheckFront(string content, int start)
        {
            if (start == 0) return true;

            return IsSpecialChar(content[start - 1]);
        }

        private bool CheckBack(string content, int end)
        {
            if (end == content.Length - 1) return true;

            return IsSpecialChar(content[end + 1]);
        }

        private bool IsSpecialChar(char c)
        {
            return SpecialChars.Contains(c);
        }

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram)o;

            Dictionary<SerializableFLFunction, (int, SerializableFLInstruction[], SerializableFLInstructionArgument[])[]
            > funcs =
                new Dictionary<SerializableFLFunction, (int, SerializableFLInstruction[],
                    SerializableFLInstructionArgument[])[]>();

            foreach (SerializableFLFunction serializableFlFunction in input.Functions)
            {
                List<(int, SerializableFLInstruction[], SerializableFLInstructionArgument[])> targetSequences =
                    new List<(int, SerializableFLInstruction[], SerializableFLInstructionArgument[])>();
                List<SerializableFLInstruction> sequence = new List<SerializableFLInstruction>();
                List<SerializableFLInstructionArgument> args = new List<SerializableFLInstructionArgument>();
                int start = 0;
                for (int i = 0; i < serializableFlFunction.Instructions.Count; i++)
                {
                    if (!CanBeOptimized(serializableFlFunction.Instructions[i].InstructionKey))
                    {
                        if (sequence.Count > 1)
                        {
                            targetSequences.Add((start, sequence.ToArray(), args.ToArray()));
                            sequence = new List<SerializableFLInstruction>();
                            args = new List<SerializableFLInstructionArgument>();
                        }
                        else
                        {
                            sequence.Clear();
                            args.Clear();
                        }

                        start = i + 1;
                    }
                    else
                    {
                        sequence.Add(serializableFlFunction.Instructions[i]);
                        args.AddRange(serializableFlFunction.Instructions[i].Arguments);
                    }
                }

                if (sequence.Count > 1)
                {
                    targetSequences.Add((start, sequence.ToArray(), args.ToArray()));
                }

                if (targetSequences.Count != 0)
                {
                    funcs[serializableFlFunction] = targetSequences.ToArray();
                }
            }

            List<(SerializableFLFunction, int, string, SerializableFLInstructionArgument[])> targetFunctions =
                new List<(SerializableFLFunction, int, string, SerializableFLInstructionArgument[])>();
            Dictionary<string, (string, CLProgram[])>
                generatedTargets = new Dictionary<string, (string, CLProgram[])>();

            foreach (KeyValuePair<SerializableFLFunction, (int, SerializableFLInstruction[],
                         SerializableFLInstructionArgument[])[]> serializableFlInstructionse in funcs)
            {
                foreach ((int, SerializableFLInstruction[], SerializableFLInstructionArgument[])
                         serializableFlInstructions in serializableFlInstructionse.Value)
                {
                    (string, string, CLProgram[]) instr = GenerateTargets(serializableFlInstructions.Item2);
                    if (!generatedTargets.ContainsKey(instr.Item1))
                    {
                        generatedTargets[instr.Item1] = (instr.Item2, instr.Item3);
                    }

                    targetFunctions.Add(
                                        (serializableFlInstructionse.Key, serializableFlInstructions.Item1, instr.Item1,
                                         serializableFlInstructions.Item3)
                                       );
                }
            }

            Dictionary<string, string> generatedKernelSource = new Dictionary<string, string>();
            foreach (KeyValuePair<string, (string, CLProgram[])> generatedTarget in generatedTargets)
            {
                generatedKernelSource[generatedTarget.Key] =
                    Merge(generatedTarget.Value.Item1, generatedTarget.Value.Item2);
            }


            for (int i = targetFunctions.Count - 1; i >= 0; i--)
            {
                (SerializableFLFunction, int, string, SerializableFLInstructionArgument[]) targetFunction =
                    targetFunctions[i];
                int length = generatedTargets[targetFunction.Item3].Item2.Length;
                int start = targetFunction.Item2;
                targetFunction.Item1.Instructions.RemoveRange(start, length);
                targetFunction.Item1.Instructions.Insert(
                                                         start,
                                                         new SerializableFLInstruction(
                                                              targetFunction.Item3,
                                                              targetFunction.Item4.ToList()
                                                             )
                                                        );
            }

            foreach (KeyValuePair<string, string> keyValuePair in generatedKernelSource)
            {
                input.KernelData.Add(new EmbeddedKernelData(keyValuePair.Key, keyValuePair.Value));
            }

            return input;
        }

        private class ProgramComparer : EqualityComparer<CLProgram>
        {

            public override bool Equals(CLProgram x, CLProgram y)
            {
                return x.ClProgramHandle.Handle == y.ClProgramHandle.Handle;
            }

            public override int GetHashCode(CLProgram obj)
            {
                return obj.ClProgramHandle.Handle.GetHashCode();
            }

        }

    }
}
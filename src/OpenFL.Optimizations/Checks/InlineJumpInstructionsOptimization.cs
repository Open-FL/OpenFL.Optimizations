using System.Linq;

using OpenFL.Core.DataObjects.SerializableDataObjects;
using OpenFL.Core.ProgramChecks;

namespace OpenFL.Optimizations.Checks
{
    public class InlineJumpInstructionsOptimization : FLProgramCheck<SerializableFLProgram>
    {

        public override FLProgramCheckType CheckType => FLProgramCheckType.Optimization;

        public override int Priority => 1;

        public override object Process(object o)
        {
            SerializableFLProgram input = (SerializableFLProgram) o;
            bool removedOne = true;
            while (removedOne)
            {
                removedOne = false;
                foreach (SerializableFLFunction f in input.Functions)
                {
                    for (int i = f.Instructions.Count - 1; i >= 0; i--)
                    {
                        SerializableFLInstruction t = f.Instructions[i];
                        if (t.InstructionKey == "jmp" &&
                            t.Arguments[0].ArgumentCategory == InstructionArgumentCategory.Function)
                        {
                            string fname = t.Arguments[0].Identifier;
                            SerializableFLFunction func = input.Functions.First(x => x.Name == fname);
                            f.Instructions.RemoveAt(i);
                            f.Instructions.InsertRange(i, func.Instructions);
                            removedOne = true;
                        }
                    }
                }
            }

            return input;
        }

    }
}
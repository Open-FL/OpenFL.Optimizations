using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using OpenFL.Core.ProgramChecks;

using PluginSystem.Core.Pointer;
using PluginSystem.Utility;

namespace OpenFL.Optimizations
{
    public class OpenFLOptimizationPlugin : APlugin<FLProgramCheckBuilder>
    {

        public override string Name => "open-fl-optimizations";

        public override void OnLoad(PluginAssemblyPointer ptr)
        {
            base.OnLoad(ptr);


            List<FLProgramCheck> checks = new List<FLProgramCheck>();
            checks.AddRange(
                            Assembly.GetExecutingAssembly().GetTypes()
                                    .Where(
                                           y => !y.IsAbstract &&
                                                typeof(FLProgramCheck)
                                                    .IsAssignableFrom(y) &&
                                                y != typeof(FLProgramCheck)
                                          )
                                    .Select(
                                            y => (FLProgramCheck)Activator
                                                .CreateInstance(y)
                                           )
                                    .Where(y => (y.CheckType & PluginHost.StartProfile) != 0)
                           );
            checks.ForEach(x => PluginHost.AddProgramCheck(x));
        }

    }
}

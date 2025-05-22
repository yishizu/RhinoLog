using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace GELTrainingLog
{
    
    public class GELTrainingLogCommand : Command
    {
        public GELTrainingLogCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static GELTrainingLogCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "GELTrainingLogCommand";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("The {0} command is under construction.", EnglishName);

            // GELTrainingLogCommand 実行時のログを記録
            var logger = GELTrainingLogPlugin.Instance;
            if (logger != null)
            {
                var user = System.Environment.UserName;
                logger.GetType().GetMethod("Log", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(logger, new object[] { "Custom Command", EnglishName });
            }

            return Result.Success;
        }
    }
}

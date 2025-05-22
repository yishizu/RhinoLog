﻿using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace GELGHTrainingLog
{
    public class GELGHTrainingLogInfo : GH_AssemblyInfo
    {
        public override string Name => "GELGHTrainingLog Info";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("14b71617-11d9-4108-89c4-179ce78762fa");

        //Return a string identifying you or your company.
        public override string AuthorName => "Yuko Ishizu / Geometry Engineering Lab";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "https://geometryengineeringlab.tech";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
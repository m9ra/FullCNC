using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    [Serializable]
    public class ReadableIdentifier
    {
        /// <summary>
        /// Readable name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Version specifier for the identifier.
        /// </summary>
        public readonly int Version;

        public ReadableIdentifier(string name, int version = 0)
        {
            Name = name;
            Version = version;
        }


        /// <summary>
        /// Creates next version of the identifier.
        /// </summary>
        public ReadableIdentifier NextVersion()
        {
            return new ReadableIdentifier(Name, Version + 1);
        }

        /// </inheritdoc>
        public override int GetHashCode()
        {
            return Name.GetHashCode() + Version.GetHashCode();
        }

        /// </inheritdoc>
        public override bool Equals(object obj)
        {
            var o = obj as ReadableIdentifier;
            if (o == null)
                return false;

            return Name.Equals(o.Name) && Version.Equals(o.Version);
        }

        /// </inheritdoc>
        public override string ToString()
        {
            if (Version == 0)
                return Name;

            return string.Format("{0} ({1})", Name, Version);
        }
    }
}

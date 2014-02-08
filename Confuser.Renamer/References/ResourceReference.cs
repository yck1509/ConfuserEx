using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;

namespace Confuser.Renamer.References
{
    class ResourceReference : INameReference<TypeDef>
    {
        Resource resource;
        TypeDef typeDef;
        string format;
        public ResourceReference(Resource resource, TypeDef typeDef, string format)
        {
            this.resource = resource;
            this.typeDef = typeDef;
            this.format = format;
        }

        public bool UpdateNameReference(ConfuserContext context, INameService service)
        {
            resource.Name = string.Format(format, typeDef.Namespace, typeDef.Name);
            return true;
        }

        public bool ShouldCancelRename()
        {
            return false;
        }
    }
}

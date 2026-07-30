using System;
using System.Linq.Expressions;

namespace Aq.ExpressionJsonSerializer
{
    partial class Serializer
    {
        private bool DebugInfoExpression(Expression expr)
        {
            // This tested ConditionalExpression until it was corrected. It never misfired,
            // because ConditionalExpression is claimed earlier in the dispatch chain, but
            // it also meant this handler never matched the node it is named for.
            var expression = expr as DebugInfoExpression;
            if (expression == null) { return false; }

            throw new NotSupportedException(
                "DebugInfoExpression cannot be serialized. It carries a SymbolDocumentInfo "
                + "(source file name, language GUID, checksum) which only conveys "
                + "sequence-point information to a debugger attached to the original "
                + "assembly. That has no meaning once the tree is rebuilt in another "
                + "process, so there is nothing useful to round-trip. Strip debug info from "
                + "the expression before serializing it.");
        }
    }
}

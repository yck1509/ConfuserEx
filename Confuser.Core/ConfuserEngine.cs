using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Confuser.Core
{
    /// <summary>
    /// The processing engine of Confuser.
    /// </summary>
    public static class ConfuserEngine
    {
        /// <summary>
        /// Runs the engine with the specified parameters.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        public static Task Run(ConfuserParameters parameters, CancellationToken? token = null)
        {
            if (token == null)
                token = new CancellationTokenSource().Token;
            return Task.Factory.StartNew(() => RunInternal(parameters, token.Value), token.Value);
        }

        /// <summary>
        /// Runs the engine.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="token">The cancellation token.</param>
        static void RunInternal(ConfuserParameters parameters, CancellationToken token)
        {
        }
    }
}

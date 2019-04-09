using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dialogBot
{
    public class Middleware : IMiddleware
    {

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default(CancellationToken))
        {

            Console.WriteLine("Antes");
            Console.WriteLine(turnContext.Activity.Text);

            await next(cancellationToken);
            
            Console.WriteLine("Depois");

        }
        

    }
}

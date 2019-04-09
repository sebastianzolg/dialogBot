// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Builder.Dialogs;
using System;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using System.Linq;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Text;

namespace dialogBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class dialogBotBot : IBot
    {
        private readonly dialogBotAccessors _accessors;
        private readonly ILogger _logger;
        private DialogSet _dialogs;
        private LuisRecognizer Recognizer { get; } = null;
        private QnAMaker QnA { get; } = null;
       
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="conversationState">The managed conversation state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>

        // public dialogBotBot(ConversationState conversationState, ILoggerFactory loggerFactory)
        public dialogBotBot(dialogBotAccessors accessors, LuisRecognizer luis, QnAMaker qna)
        {
            Console.WriteLine("Bot constructor");
            // Set the _accessors 
            _accessors = accessors ?? throw new ArgumentNullException(nameof(accessors));
            // The DialogSet needs a DialogState accessor, it will call it when it has a turn context.
            _dialogs = new DialogSet(accessors.ConversationDialogState);
            // This array defines how the Waterfall will execute.
            var waterfallSteps = new WaterfallStep[]
            {
                NameStepAsync,
                NameConfirmStepAsync,
            };

            // The incoming luis variable is the LUIS Recognizer we added above.
            this.Recognizer = luis ?? throw new System.ArgumentNullException(nameof(luis));

            // The incoming QnA variable is the QnAMaker we added above.
            this.QnA = qna ?? throw new System.ArgumentNullException(nameof(qna));

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            _dialogs.Add(new WaterfallDialog("details", waterfallSteps));
            _dialogs.Add(new TextPrompt("name"));

        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            Console.WriteLine("Bot onturnasync");
            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Check LUIS model
                var recognizerResult = await this.Recognizer.RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizerResult?.GetTopScoringIntent();
                // Get the Intent as a string
                string strIntent = (topIntent != null) ? topIntent.Value.intent : "";
                // Get the IntentScore as a double
                double dblIntentScore = (topIntent != null) ? topIntent.Value.score : 0.0;
                // Only proceed with LUIS if there is an Intent 
                // and the score for the Intent is greater than 95
                if (strIntent != "" && (dblIntentScore > 0.84))
                {
                    switch (strIntent)
                    {
                        case "None":
                            //add the bot response to contexto
                            await turnContext.SendActivityAsync("Desculpa, não percebi.");
                            break;
                        case "Utilities_Help":
                            //add the bot response to contexto
                            await turnContext.SendActivityAsync("Quero-te ajudar!\nO que precisas?");
                            break;
                        case "HomeAutomation_TurnOff":
                            //add the bot response to contexto
                            await turnContext.SendActivityAsync("Vou te bloquear o dispositivo");
                            Thread.Sleep(200);
                            System.Diagnostics.Process.Start(@"C:\WINDOWS\system32\rundll32.exe", "user32.dll,LockWorkStation");
                            break;
                        case "Communication_SendEmail":
                            //add the bot response to contexto
                            await turnContext.SendActivityAsync("Ainda não sei mandar emails");
                            break;
                        default:
                            // Received an intent we didn't expect, so send its name and score.
                            //add the bot response to contexto
                            await turnContext.SendActivityAsync($"Intent: {topIntent.Value.intent} ({topIntent.Value.score}).");
                            break;
                    }
                }
                else
                {
                    // Get the conversation state from the turn context.
                    var state = await _accessors.CounterState.GetAsync(turnContext, () => new CounterState());

                    // Bump the turn count for this conversation.
                    state.TurnCount++;

                    //Get the user state
                    var user = await _accessors.UserProfile.GetAsync(turnContext, () => new UserProfile());
                    if (user.Name == null)
                    {
                        // Run the DialogSet - let the framework identify the current state of the dialog from
                        // the dialog stack and figure out what (if any) is the active dialog.
                        var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                        var results = await dialogContext.ContinueDialogAsync(cancellationToken);
                        // If the DialogTurnStatus is Empty we should start a new dialog.
                        if (results.Status == DialogTurnStatus.Empty)
                        {
                            await dialogContext.BeginDialogAsync("details", null, cancellationToken);
                        }
                    }
                    else
                    {
                        var answers = await this.QnA.GetAnswersAsync(turnContext);
                        if (answers.Any() && answers[0].Score > 0.8)
                        {
                            // If the service produced one or more answers, send the first one.
                            await turnContext.SendActivityAsync(answers[0].Answer);
                        }
                        else
                        {
                            var responseMessage = $"Ainda não sei a resposta mas vou averiguar\nPosso-te ajudar com mais alguma coisa?";
                            /*
                            String connectionString = "datasource=127.0.0.1;port=3306;username=root;password=;database=botdatabase";
                            MySqlConnection connection = new MySqlConnection(connectionString);

                            String query = "Insert into mensagem(texto,contexto) values()";
                            */
                            await turnContext.SendActivityAsync(responseMessage);
                        }
                    }

                    // Save the user profile updates into the user state.
                    await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);

                    // Set the property using the accessor.
                    await _accessors.CounterState.SetAsync(turnContext, state);

                    // Save the new turn count into the conversation state.
                    await _accessors.ConversationState.SaveChangesAsync(turnContext);
                }
            }
        }

        //Ask the user's name
        private static async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Running a prompt here means the next WaterfallStep will be 
            // run when the users response is received.
            return await stepContext.PromptAsync("name", new PromptOptions { Prompt = MessageFactory.Text("Olá!\nComo te chamas?") }, cancellationToken);
        }

        //Get's the name and greet's the user
        private async Task<DialogTurnResult> NameConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // We can send messages to the user at any point in the WaterfallStep.
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Olá {stepContext.Result} , é bom conhecer-te!\nEm que te posso ajudar?"), cancellationToken);

            //Get the current user profile
            var userProfile = await _accessors.UserProfile.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);

            //Update the profile
            userProfile.Name = (string)stepContext.Result;

            // WaterfallStep always finishes with the end of the Waterfall or with another dialog, here it is the end.
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
        
    }
}

module sample_dictionary_post.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks.V2

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

[<CLIMutable>] // fix MissingMethodException: No parameterless constructor defined for this object.
type AdventurerDto = 
    {
        name: string
        inventory: Dictionary<string,int>
        // string = item id guid
        // int = how many of that item
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "sample_dictionary_post" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "sample_dictionary_post" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

    let someFormSubmitPage = 
        [
            partial()
            p [] [
                let formFields = 
                    [
                        input [_type "text"; _name "name"; _value "" ;_placeholder "This will bind to 'name' value"]
                        // If I use the below 2 i get Missing value for required property inventory.
                        //input [_type "text"; _name "inventory[key]"; _value "itemid123";_placeholder "This will be the inventory dictionary key"]
                        //input [_type "text"; _name "inventory[value]"; _value "1";_placeholder "This will be the inventory dictionary value"]
                        
                        // if I do it as a single line i get
                        // ArgumentException: Object of type 'System.String' cannot be converted to type 'System.Collections.Generic.Dictionary`2[System.String,System.Int32]'.
                        input [_type "text"; _name "inventory[]"; _value "itemid123,2";_placeholder "This will be the inventory dictionary key + value"]
                        input [_type "submit"; _value "Submit"]
                    ]
                form [ _method "post"; _action "/createAdventurer"] formFields
            ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

let postPage = 
    htmlView Views.someFormSubmitPage

let postHandler = 
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
                match! ctx.TryBindFormAsync<AdventurerDto>() with
                |Ok abc ->
                    return! text "Yay it worked!" next ctx
                |Error e -> return! text e next ctx
            }

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                route "/post" >=> postPage
                routef "/hello/%s" indexHandler
            ]
        POST >=> choose [
            route "/createAdventurer" >=> postHandler
        ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l.Equals LogLevel.Error)
           .AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
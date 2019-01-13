module Server
open Giraffe.HttpStatusCodeHandlers

open System.IO
open System.Threading.Tasks


open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks.V2
open Giraffe
open Saturn
open Shared
open Service
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open System
open Microsoft.WindowsAzure.Storage
open Config

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x
let publicPath = tryGetEnv "public_path" |> Option.defaultValue "../Client/public" |> Path.GetFullPath
let storageAccount = tryGetEnv "STORAGE_CONNECTIONSTRING" |> Option.defaultValue "UseDevelopmentStorage=true" |> CloudStorageAccount.Parse
let port = 8085us

// let getInitCounter() : Task<Counter> = task { return { Value = 42 } }

let webApp = router {
    get "/api/init" (fun next ctx ->
        task {
            
            let! counter = Service.getInitCounterAsync(Controller.getConfig ctx)
            return! Successful.OK { Value = counter } next ctx
        })
}

let configureSerialization (services:IServiceCollection) =
    services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>(Thoth.Json.Giraffe.ThothSerializer())

let configureContextAccessor (services: IServiceCollection) =
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>()


let configureAzure (services:IServiceCollection) =
    tryGetEnv "APPINSIGHTS_INSTRUMENTATIONKEY"
    |> Option.map services.AddApplicationInsightsTelemetry
    |> Option.defaultValue services

let setupConfiguration () =
    let ctx = HttpContextAccessor() 
    let config = ctx.HttpContext.GetService<IConfiguration>()
    { github = config.["github"]; githubUserAgent = config.["githubUserAgent"] }

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    service_config configureSerialization
    service_config configureContextAccessor
    service_config configureAzure
    use_config setupConfiguration
    use_gzip 
}

run app

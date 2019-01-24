module Service

open System
open FSharp.Data
open FSharp.Data.HttpRequestHeaders
open Shared
open Config
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Configuration

// GitHub.json downloaded from 
// https://api.github.com/repos/fsharp/FSharp.Data/issues 
// to prevent rate limit when generating these docs
// type MyClient = GraphQLProvider<"https://api.github.com/graphql", "modified.json">

[<Literal>]
let freeQuery = """{
  repository(owner: \"Xamarin\", name: \"Xamarin.Forms\") { 
    project(number: 3) {
      columns(first: 10) {
        nodes {  
          name
          cards(first: 100) {
            nodes {
              content {
                ... on Issue {
                  updatedAt
                  createdAt
                  number
                  title
                  labels(first: 10) {
                    nodes {
                      name
                    }
                  }
                  comments(last: 1) {
                    nodes {
                      author {
                        login
                      }
                      bodyText
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}"""


[<Literal>]
let jsonQuery = "{\"query\": \"" + freeQuery + "\"}"
type TestProvider = JsonProvider<jsonQuery>
type TriageProvider = JsonProvider<"triage.json">



//let getInitCounter() : 
let getInitCounter (config:Config) =   
  let authString = "bearer " + config.github
  let response = 
    TestProvider.GetSample().JsonValue.Request ( "https://api.github.com/graphql",
      headers = [ UserAgent config.githubUserAgent; Accept HttpContentTypes.Json; Authorization authString ],
      httpMethod = "POST"
  )

  let retrieveString = 
    fun body -> match body with
                | Text text -> text
                | Binary binary -> failwithf "Expecting text, but got a binary response (%d bytes)" binary.Length

  let thing = retrieveString response.Body
  let triageResult = TriageProvider.Parse(thing)
  // Console.WriteLine(thing)
  triageResult.Data.Repository.Project.Columns.Nodes
    |> Seq.filter (fun x -> Set.contains x.Name activeColumns)
    |> Seq.where
        (fun ds ->
            ds.Cards.Nodes
            |> Seq.exists
                (fun item ->
                    match item.Content.JsonValue.TryGetProperty("labels") with
                      | Some _ -> item.Content.Labels.Nodes
                                        |> Seq.exists (fun label -> not (Set.contains label.Name labelsToSkip))
                      | None -> true
                )
        )
    |> Seq.map
        (fun x ->
            x.Cards.Nodes
            |> Seq.map (fun card ->
                {
                    ColumnName = x.Name;
                    Issue =
                    {
                      Title = card.Content.Title;
                      Labels= match card.Content.JsonValue.TryGetProperty("labels") with
                                      | Some _ -> card.Content.Labels.Nodes |> Seq.map(fun label -> label.Name) |> Seq.toList
                                      | None -> List.empty<string>
                      Comments = match card.Content.JsonValue.TryGetProperty("comments") with
                                      | Some _ -> card.Content.Comments.Nodes |> Seq.map(fun comment -> { Author = comment.Author.Login; BodyText = comment.BodyText  }) |> Seq.toList
                                      | None -> List.empty<Comment>
                      
                      
                      //card.Content.Comments.Nodes |> Seq.map(fun comment -> { Author = comment.Author.Login; BodyText = comment.BodyText  }) |> Seq.toList;
                      Number = match card.Content.JsonValue.TryGetProperty("number") with
                                      | Some _ -> card.Content.Number
                                      | None -> -1;
                      UpdatedAt = match card.Content.JsonValue.TryGetProperty("updatedAt") with
                                      | Some _ -> card.Content.UpdatedAt
                                      | None -> DateTimeOffset.MinValue;
                      CreatedAt = match card.Content.JsonValue.TryGetProperty("createdAt") with
                                      | Some _ -> card.Content.CreatedAt
                                      | None -> DateTimeOffset.MinValue;
                    }
                })
        )
    |> Seq.concat
    |> Seq.toList


let getInitCounterAsync (config:Config) = task {            
            return getInitCounter(config)
        }
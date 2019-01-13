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

  triageResult.Data.Repository.Project.Columns.Nodes
    |> Seq.filter (fun x -> Set.contains x.Name activeColumns)
    |> Seq.where
        (fun ds ->
            ds.Cards.Nodes
            |> Seq.exists
                (fun item ->
                    item.Content.Labels.Nodes
                    |> Seq.exists (fun label -> not (Set.contains label.Name labelsToSkip))
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
                      Labels=card.Content.Labels.Nodes |> Seq.map(fun label -> label.Name) |> Seq.toList;
                      Comments=card.Content.Comments.Nodes |> Seq.map(fun comment -> { Author = comment.Author.Login; BodyText = comment.BodyText  }) |> Seq.toList;
                      Number=card.Content.Number;
                      UpdatedAt=card.Content.UpdatedAt;
                      CreatedAt=card.Content.CreatedAt;
                    }
                })
        )
    |> Seq.concat
    |> Seq.toList


let getInitCounterAsync (config:Config) = task {            
            return getInitCounter(config)
        }
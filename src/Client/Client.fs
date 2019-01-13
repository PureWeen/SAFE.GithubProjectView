module Client


open Elmish
open Elmish.React

open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch

open Thoth.Json

open Shared


open Fulma
open System

open Fable.Core.JsInterop
open Fable.PowerPack
open Fulma.Extensions.Wikiki
open Fable.Import.React


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = { Counter: Counter option; ExceptionMessage: string; FilterNeedsInfo: bool; PageLoading: bool; }

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
| InitialCountLoaded of Result<Counter, exn>
| LoadFailed of Result<Counter, exn>
| Refresh
| RefreshSuccess of Counter
| RefreshFailed of Exception
| FilterNeedsInfo of bool

let refreshTriageData() = fetchAs<Counter> "/api/init" (Decode.Auto.generateDecoder()) 
let initialCounter = refreshTriageData()

let initialCounterForMessages (randomArgument) = refreshTriageData() []


// defines the initial state and initial command (= side-effect) of the application
let init () : Model * Cmd<Msg> =
    let initialModel = { Counter = None; ExceptionMessage = "Loading..."; FilterNeedsInfo = false; PageLoading = true }
    let loadCountCmd =
        Cmd.ofPromise
            initialCounter
            []
            (Ok >> InitialCountLoaded)
            (Error >> LoadFailed)
    initialModel, loadCountCmd




// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match currentModel.Counter, msg with
    | _, InitialCountLoaded (Ok initialCount)->
        let nextModel = { currentModel with Counter = Some initialCount; ExceptionMessage = ""; PageLoading=false}
        nextModel, Cmd.none
    | _, LoadFailed (Error initialCount)->
        let nextModel = { currentModel with ExceptionMessage = initialCount.Message; PageLoading=false}
        nextModel, Cmd.none
    | Some counter, Refresh ->
        let nextModel = { currentModel with ExceptionMessage = "Refreshing"}
        nextModel, Cmd.ofPromise initialCounterForMessages "" RefreshSuccess RefreshFailed
    | Some counter, RefreshSuccess (somemessage) ->
        let nextModel = { currentModel with Counter = Some somemessage; ExceptionMessage = "Refresh Success";}
        nextModel, Cmd.none
    | Some counter, RefreshFailed (somemessage) ->
        let nextModel = { currentModel with ExceptionMessage = somemessage.Message;}
        nextModel, Cmd.none
    | Some counter, FilterNeedsInfo (isChecked) ->
        // not sure how to retrieved the checked property off the input :-/ 
        let nextModel = { currentModel with FilterNeedsInfo = isChecked }
        nextModel, Cmd.none
    | _ -> 
        currentModel, Cmd.none


let safeComponents =
    let components =
        span [ ]
           [
             a [ Href "https://saturnframework.github.io" ] [ str "Saturn" ]
             str ", "
             a [ Href "http://fable.io" ] [ str "Fable" ]
             str ", "
             a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
             str ", "
             a [ Href "https://mangelmaxime.github.io/Fulma" ] [ str "Fulma" ]
           ]

    p [ ]
        [ strong [] [ str "SAFE Template" ]
          str " powered by: "
          components ]

let getTableData (model:Model) =  

    let counter = model.Counter.Value;
    
    let lastCommentAuthor (comments:list<Comment>) = 
        match List.tryLast comments with
        | Some x -> x.Author
        | None -> null
    
    let lastComment (comments:list<Comment>) = 
        match List.tryLast comments with
        | Some x -> x.BodyText
        | None -> null

    let getNeedsInfoSuggestion(issue: Issue) = 
        match issue  with
            | x when x.UpdatedAt.Date <= System.DateTime.UtcNow.Date.AddDays(float -30) -> "should be closed"
            | _ -> "No suggestion"

    let getIssueSuggestion(issue: ProjectCardIssue) =
        let labelSuggestion = 
            (match issue.Issue.Labels  with
                | x when x |> Seq.exists (fun label -> (Shared.platformLabels |> Seq.exists(fun platformLabel -> label.StartsWith(platformLabel) ))) -> ""
                | _ -> "Needs Platform Label. ")
                
        match issue.ColumnName  with
            | "Needs Info" -> labelSuggestion + getNeedsInfoSuggestion issue.Issue
            | "New" -> labelSuggestion + ("Issue Created " + Math.Round(DateTimeOffset.Now.Subtract(issue.Issue.CreatedAt).TotalDays,2).ToString() + " days ago")
            | _ -> labelSuggestion

    let toolTipAuthor (pc) =
        match lastCommentAuthor(pc.Issue.Comments)  with
            | null -> td [ ] [ str "No Comment" ]
            | _ -> td [ Class (Tooltip.ClassName + " " + Tooltip.IsTooltipLeft + " " + Tooltip.IsMultiline) ; Tooltip.dataTooltip (lastComment(pc.Issue.Comments)) ] 
                    [str (lastCommentAuthor(pc.Issue.Comments)) ]    

    let reactComponent = 
        counter.Value
        |> Seq.filter (fun pc -> not model.FilterNeedsInfo || not (Seq.contains (lastCommentAuthor(pc.Issue.Comments)) Shared.teamMembers))
        |> Seq.map(fun pc -> 
                tr [ ]
                 [ 
                   td [ ] [ Button.a [  Button.Props [ Target "_blank"; Href ("https://github.com/xamarin/Xamarin.Forms/issues/" + pc.Issue.Number.ToString()) ] ] [ str (pc.Issue.Number.ToString()) ]  ]
                   td [ ] [ str pc.ColumnName ]
                   td [ ] [ str pc.Issue.Title ]
                   td [ ] [ str ("Updated " + Math.Round(DateTimeOffset.Now.Subtract(pc.Issue.UpdatedAt).TotalDays,2).ToString() + " days ago") ]
                   td [ ] [ toolTipAuthor(pc)  ]
                   td [ ] [ str (getIssueSuggestion(pc))  ] ]
              )
        |> tbody [ ]          

    Table.table [ Table.IsHoverable ]
        [ thead [ ]
            [ tr [ ]
                [ th [ ] [ str "Number" ]
                  th [ ] [ str "Column" ]                  
                  th [ ] [ str "Title" ]
                  th [ ] [ str "Updated At" ]
                  th [ ] [ str "Last Comment" ]
                  th [ ] [ str "Suggestion" ] ] ]
          reactComponent ]


let show = function
// need to figure out cleaner way to do this
| { Counter = Some counter; FilterNeedsInfo = filterNeedsInfo;  } -> getTableData { Counter = Some counter; FilterNeedsInfo = filterNeedsInfo; ExceptionMessage = ""; PageLoading = false  }
| { Counter = None   } ->  div [] []

let button txt onClick =
    Button.button
        [ Button.IsFullWidth
          Button.Color IsPrimary
          Button.OnClick onClick ]
        [ str txt ]
let rnd = System.Random()
 
let checkbox txt onClick =
    Checkbox.checkbox[][ Checkbox.input[Props [ OnClick onClick ] ];str txt]

let view (model : Model) (dispatch : Msg -> unit) =    
    div []        
        [ 
          PageLoader.pageLoader [ PageLoader.Color IsSuccess; PageLoader.IsActive model.PageLoading ] [ str randomQuotes.[rnd.Next(0,randomQuotes.Length)] ]
          Navbar.navbar [ Navbar.Color IsPrimary ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ ]
                    [ str "Triage" ] ] ]

          Container.container []
            [ Content.content [ ]
                [ button "Refresh" (fun _ -> dispatch Refresh)
                  checkbox "Remove Need Info Last Comment From Team" (fun ev -> dispatch (FilterNeedsInfo !!ev.target?``checked``))
                  div [] [str model.ExceptionMessage]                  
                  show model ] ]
                        

          Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif
|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run

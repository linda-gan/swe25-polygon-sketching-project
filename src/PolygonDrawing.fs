// Example setup for the polygon drawing project.
// The application logics should be implemented in the `updateModel` function.
// The undo-redo-relevant parts should be implemented in `addUndoRedo`.abs
// Please note that, the logics is easiest implemented when always adding elements
// to the beginning of the list e.g. build polylines and polygons in reverse order.
module PolygonDrawing 

open Fable.Core
open Feliz
open Elmish

// we use a record here, a tuple could also serve the purpose though
type Coord = { x : float; y : float }

// "polygon" line. Each list element describes the respective vertex.
// note that we could use a record here, but a type-alias is more lightweight
// and serves its purpose.
// I recommend storing the coordinates in reverse order, so that each vertex gets prepended
// to the list. This way, adding new vertices is O(1).
type PolyLine = list<Coord>

type Model = {
    // all "finished" polygons, created so far, by convention, new PolyLines can be prepended to this list to
    // make additions efficient and the code more elegant.
    finishedPolygons : list<PolyLine>
    // the polygon, we are currently working on (and extending, vertex-by-vertex). Having the current
    // one explicitly as opposed to already in the finishedPolygons list makes the code a bit more elegant
    // and approachable
    currentPolygon : Option<PolyLine>
    // current positon of the mouse (to draw a preview)
    mousePos : Option<Coord>
    // optionally, the model before this current state (note, that this immutable!), used for redo
    past : Option<Model>
    // used for redo
    future : Option<Model>
}

// and explicit representation of all possible user interactions. This one can be used for 
// automatic testing or storing interaction logs to disk
type Msg =
    | AddPoint of Coord
    | SetCursorPos of Option<Coord>
    | FinishPolygon
    | Undo
    | Redo

// creates the initial model, which is used when creating the interactive application (see Main.fs)
let init () =
    let m = 
        { finishedPolygons = []; currentPolygon = None; // records can be written multiline
          mousePos = None ; past = None; future = None }
    m, Cmd.none // Cmd is optionally to explicitly represent side effects in a safe manner (here we don't bother)


let updateModel (msg : Msg) (model : Model) =
    match msg with
    | AddPoint p ->
        let newPoint =
            match model.currentPolygon with
            | None -> [p]
            | Some points -> p :: points
        { model with currentPolygon = Some newPoint }
        
    | FinishPolygon ->
        match model.currentPolygon with
        | None -> model
        | Some poly ->
            { model with
                currentPolygon = None
                finishedPolygons = poly :: model.finishedPolygons
            }

    | _ -> model

// wraps an update function with undo/redo.
let addUndoRedo (updateFunction : Msg -> Model -> Model) (msg : Msg) (model : Model) =
    // first let us, handle the cursor position, which is not undoable, and handle undo/redo messages
    // in a next step we actually run the "core" system logics.
    match msg with
    | SetCursorPos p -> 
        // update the mouse position and create a new model.
        { model with mousePos = p }
    | Undo -> 
        // state with it.
        match model.past with
        | None -> model
        | Some previousModel ->
            { previousModel with 
                future = Some model
                mousePos = model.mousePos
            }
        
    | Redo -> 
        match model.future with
        | None -> model
        | Some nextModel ->
            { nextModel with 
                past = Some model 
                mousePos = model.mousePos
            }
        
    | _ -> 
        // use the provided update function for all remaining messages
        let newModel = updateFunction msg model
        { newModel with
            past = Some model
            future = None 
        }


let update (msg : Msg) (model : Model)  =
    let newModel = addUndoRedo updateModel msg model
    newModel, Cmd.none

[<Emit("getSvgCoordinates($0)")>] // wrapper to use the getSvgCoordinates JS function (provided by index.html) from f# here typesafely.
let getSvgCoordinates (o: Browser.Types.MouseEvent): Coord = jsNative

let viewPolygon (color : string) (points : PolyLine) =
    points 
    |> List.pairwise 
    |> List.map (fun (c0,c1) ->
        Svg.line [
            svg.x1 c0.x; svg.y1 c0.y
            svg.x2 c1.x; svg.y2 c1.y
            svg.stroke(color)
            svg.strokeWidth 2.0
            svg.strokeLineJoin "round"
        ]
    )
 

let render (model : Model) (dispatch : Msg -> unit) =
    let border = 
        Svg.rect [ // I used ; to group together attributes semantically.
            svg.x1 0; svg.x2 500
            svg.y1 0; svg.y2 500
            svg.width 500; svg.height 500
            svg.stroke("black"); svg.strokeWidth(2); svg.fill "none"
        ] 

    // collect all svg elements of all finished polygons
    let finishedPolygons = 
        model.finishedPolygons |> List.collect (viewPolygon "green")
    let currentPolygon =
        match model.currentPolygon with
        | None -> [] // if we have no polygon, create empty svg list
        | Some p -> 
            match model.mousePos with
            | None -> 
                viewPolygon "red" p
            | Some preview -> 
                // if we have a current mouse position, prepend the mouse position to the resulting polygon
                viewPolygon "red" (preview :: p)
 
    let svgElements = List.concat [finishedPolygons; currentPolygon]

    Html.div [
        prop.style [style.custom("userSelect","none")]
        prop.children [
            Html.h1 "Simplest drawing"
            Html.button [
                prop.style [style.margin 20]; 
                prop.onClick (fun _ -> dispatch Undo)
                prop.children [Html.text "undo"]
            ]
            Html.button [
                prop.style [style.margin 20]
                prop.onClick (fun _ -> dispatch Redo)
                prop.children [Html.text "redo"]
            ]
            Html.br []
            Svg.svg [
                svg.width 500; svg.height 500
                svg.onMouseMove (fun mouseEvent -> 
                    // compute SVG relative coordinates, using JavaScript function
                    let pos = getSvgCoordinates mouseEvent

                    // fable requires to "send" messages via side effect. 
                    // Can be moved into UI system, e.g. see  https://elm-lang.org/examples/buttons
                    dispatch (SetCursorPos (Some pos))
                )
                svg.onClick (fun mouseEvent -> 
                    // create messages (purely descriptive)
                    let msgs = 
                        if mouseEvent.detail = 1 then
                            let pos = getSvgCoordinates mouseEvent
                            [AddPoint pos] 
                        elif mouseEvent.detail = 2 then
                            [FinishPolygon]
                        else
                            []

                    // fable requires to "send" messages via side effect. 
                    // Can be moved into UI system, e.g. see  https://elm-lang.org/examples/buttons
                    msgs |> List.iter dispatch
                )
                svg.children (border :: svgElements) // use : to prepend the border to the other elements
            ]
        ]
    ]
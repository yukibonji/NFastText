﻿// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
open NFastText
open NFastText.FastTextM
open System.IO

let streamToWords (s:Stream) =
        let r = new StreamReader(s)
        seq{
        let mutable line = r.ReadLine()
        while line <> null do
            yield! line.Split([|' '; '\t'; '\n'; '\v'; '\f'; '\r'|])
            line <- r.ReadLine()
        }

let split length (xs: seq<'T>) =
    let rec loop xs =
        seq{
            yield Seq.truncate length xs 
            match Seq.length xs <= length with
            | false -> yield! loop (Seq.skip length xs)
            | true -> ()
        }
    loop xs

let MAX_LINE_SIZE = 1024
let rec streamToLines model (s:Stream) fromStartOnEof = 
    let r = new StreamReader(s)
    let rec loop() =
        let max_line_size = if model <> Args.model_name.sup
                            then MAX_LINE_SIZE
                            else System.Int32.MaxValue
        seq{
                let mutable line = r.ReadLine()
                while line <> null do
                    let lnWords = line.Split([|' '; '\t'; '\n'; '\v'; '\f'; '\r'|])
                    for chunk in split max_line_size lnWords do
                        yield chunk 
                    line <- r.ReadLine()

                if fromStartOnEof 
                then s.Position <- 0L
                     yield! loop()
                }
    loop()

let getVectors state rng (stream:Stream) =
        use cin = System.Console.OpenStandardInput()
        if state.args_.model = Args.model_name.sup 
        then let src = streamToLines state.args_.model stream false
             NFastText.FastTextM.textVectors state rng src
        else let words = streamToWords stream
             NFastText.FastTextM.wordVectors state words

let trainArgs = {
        input = "D:/ft/data/dbpedia.train"
        output = "D:/ft/result/dbpedia"
        args = { Args.supervisedArgs with
                    dim=10
                    lr = 0.1f
                    wordNgrams = 2
                    minCount = 1
                    bucket = 10000000
                    epoch = 5
               }
        thread =  4
}
let label = "__label__"
let verbose = 2
let train() =
    let output = "D:/ft/result/dbpedia"
    let state = FastTextM.createState label verbose
    let stream = System.IO.File.Open(trainArgs.input, FileMode.Open, FileAccess.Read, FileShare.Read)
    let words = streamToWords stream
    let state, _ = FastTextM.train state label verbose words trainArgs streamToLines
    FastTextM.saveState (output + ".bin") state 
    if state.args_.model <> Args.model_name.sup 
    then FastTextM.saveVectors(state, output)

//    let getVectors model (fasttext : FastText) =
//        fasttext.loadModel(model)
//        fasttext.getVectors()



let test() =
    let state = FastTextM.loadState("D:/ft/result/dbpedia.bin",label,verbose)

    let stream = System.IO.File.Open("D:/ft/data/dbpedia.test", FileMode.Open, FileAccess.Read, FileShare.Read)
    let src = streamToLines state.args_.model stream false
    let model = FastTextM.createModel state
    let r = FastTextM.test(state,model, src,1)
    assert(r.precision >= 0.98f) 
    assert(r.recall >= 0.98f)
    assert(r.nexamples = 70000) 
let predictRes = [|
    "__label__9"
    "__label__9"
    "__label__3"
    "__label__6"
    "__label__7"
    "__label__7"
    "__label__11"
    "__label__11"
    "__label__9"
    "__label__13"
    "__label__12"
    "__label__2"
|]


let predict() =
    let state = FastTextM.loadState("D:/ft/result/dbpedia.bin",label,verbose)

    let stream = System.IO.File.Open("D:/ft/data/dbpedia.test", FileMode.Open, FileAccess.Read, FileShare.Read)
    let src = streamToLines state.args_.model stream false
    let model = FastTextM.createModel state
    let r = FastTextM.predict(state, model, src,1)
    let r = Seq.take (predictRes.Length) r 
                |> Seq.choose id
                |> Seq.map (List.head >> fst)
                |> Array.ofSeq
    assert(r = predictRes)

[<EntryPoint>]
let main argv = 
    train()
    test()
    predict()
    0 // return an integer exit code
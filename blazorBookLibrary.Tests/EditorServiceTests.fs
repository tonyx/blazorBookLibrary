module EditorServiceTests

open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open System.Threading

[<Tests>]
let tests =
    testList "editor service" [
        testCaseTask "create and retrieve an editor" <| fun _ -> task {
            setUp()
            let editorService = getEditorService()
            let editor = Editor.NewWithoutDate TenantId.Default (Name.New "Penguin Classics")

            let! addEditor = editorService.AddEditorAsync(adminContext, editor)
            Expect.isOk addEditor "should be ok"
            let! getEditor = editorService.GetEditorAsync(adminContext, editor.EditorId)
            Expect.isOk getEditor "should be ok"
            let retrieved = getEditor |> Result.get
            Expect.equal retrieved.Name editor.Name "name should match"
        }

        testCaseTask "anonymous user can retrieve an editor in default tenant" <| fun _ -> task {
            setUp()
            let editorService = getEditorService()
            let editor = Editor.NewWithoutDate TenantId.Default (Name.New "HarperCollins")

            let! addEditor = editorService.AddEditorAsync(adminContext, editor)
            Expect.isOk addEditor "should be ok"
            let! getEditor = editorService.GetEditorAsync(UserContext.Anonymous, editor.EditorId)
            Expect.isOk getEditor "should be ok"
            let retrieved = getEditor |> Result.get
            Expect.equal retrieved.Name editor.Name "name should match"
        }

        testCaseTask "rename an editor" <| fun _ -> task {
            setUp()
            let editorService = getEditorService()
            let editor = Editor.NewWithoutDate TenantId.Default (Name.New "Vintage")

            let! addEditor = editorService.AddEditorAsync(adminContext, editor)
            Expect.isOk addEditor "should be ok"

            let newName = Name.New "Vintage Books"
            let! renameResult = editorService.RenameAsync(adminContext, editor.EditorId, newName)
            Expect.isOk renameResult "should be ok"

            let! getEditor = editorService.GetEditorAsync(adminContext, editor.EditorId)
            Expect.isOk getEditor "should be ok"
            let retrieved = getEditor |> Result.get
            Expect.equal retrieved.Name newName "renamed editor should have new name"
        }

        testCaseTask "get all editors returns created editors" <| fun _ -> task {
            setUp()
            let editorService = getEditorService()
            let editor1 = Editor.NewWithoutDate TenantId.Default (Name.New "Editor One")
            let editor2 = Editor.NewWithoutDate TenantId.Default (Name.New "Editor Two")

            let! add1 = editorService.AddEditorAsync(adminContext, editor1)
            Expect.isOk add1 "should be ok"
            let! add2 = editorService.AddEditorAsync(adminContext, editor2)
            Expect.isOk add2 "should be ok"

            let! allResult = editorService.GetAllAsync(UserContext.Anonymous)
            Expect.isOk allResult "should be ok"
            let editors = allResult |> Result.get
            Expect.isTrue (editors |> List.exists (fun e -> e.EditorId = editor1.EditorId)) "should contain editor1"
            Expect.isTrue (editors |> List.exists (fun e -> e.EditorId = editor2.EditorId)) "should contain editor2"
        }
    ]

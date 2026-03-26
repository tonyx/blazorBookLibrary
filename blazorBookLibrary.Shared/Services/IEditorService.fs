namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IEditorService =
    abstract member AddEditorAsync: editor: Editor * ?ct: CancellationToken -> Task<Result<Editor,string>>
    abstract member GetEditorAsync: id: EditorId * ?ct: CancellationToken -> Task<Result<Editor,string>>
    abstract member RenameAsync: editorId: EditorId * newName: Name * ?ct: CancellationToken -> Task<Result<Editor,string>>

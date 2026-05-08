namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type IEditorService =
    abstract member AddEditorAsync: context:UserContext * editor: Editor *  ?ct: CancellationToken -> Task<Result<Editor,string>>
    abstract member GetEditorAsync: context:UserContext * id: EditorId *  ?ct: CancellationToken -> Task<Result<Editor,string>>
    abstract member RenameAsync: context:UserContext * editorId: EditorId * newName: Name *  ?ct: CancellationToken -> Task<Result<Editor,string>>

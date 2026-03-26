namespace BookLibrary.Shared.Services

open System.Threading.Tasks
open System.Collections.Generic

type IImportService = 
    abstract member ImportBooksFromGoogleAsync : string list * System.Threading.CancellationToken option -> Task<int>
    abstract member ImportBooksFromGoogleAsync : string * System.Threading.CancellationToken option -> Task<int>
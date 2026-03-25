
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Definitions
open Sharpino.Core
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IAuthorService =
    abstract member AddAuthorAsync : author: Author * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAuthorAsync : id: AuthorId * ?ct: CancellationToken -> Task<Result<Author, string>>
    abstract member RenameAsync : authorId: AuthorId * name: Name * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member UpdateIsniAsync : authorId: AuthorId * isni: Isni * ?ct: CancellationToken -> TaskResult<unit, string>


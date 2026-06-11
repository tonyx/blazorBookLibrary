namespace BookLibrary.Shared.Services

open System.Threading.Tasks

type ICookieService =
    abstract member GetCookieAsync: key: string -> Task<string option>
    abstract member SetCookieAsync: key: string * value: string * ?days: int -> Task<unit>
    abstract member DeleteCookieAsync: key: string -> Task<unit>

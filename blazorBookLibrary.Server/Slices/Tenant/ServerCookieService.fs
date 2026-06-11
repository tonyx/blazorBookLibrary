namespace BookLibrary.Services

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open BookLibrary.Shared.Services

type ServerCookieService(httpContextAccessor: IHttpContextAccessor) =
    interface ICookieService with
        member this.GetCookieAsync(key: string) =
            task {
                let context = httpContextAccessor.HttpContext
                if not (isNull context) && context.Request.Cookies.ContainsKey(key) then
                    return Some (context.Request.Cookies.[key])
                else
                    return None
            }

        member this.SetCookieAsync(key: string, value: string, ?days: int) =
            task {
                let context = httpContextAccessor.HttpContext
                if not (isNull context) && not context.Response.HasStarted then
                    let options = CookieOptions()
                    options.HttpOnly <- false
                    options.Secure <- true
                    options.SameSite <- SameSiteMode.Lax
                    match days with
                    | Some d -> options.Expires <- Nullable (DateTimeOffset.UtcNow.AddDays(float d))
                    | None -> ()
                    context.Response.Cookies.Append(key, value, options)
            }

        member this.DeleteCookieAsync(key: string) =
            task {
                let context = httpContextAccessor.HttpContext
                if not (isNull context) && not context.Response.HasStarted then
                    context.Response.Cookies.Delete(key)
            }

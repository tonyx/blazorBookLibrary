namespace blazorBookLibrary.Shared.Resources

open System.Resources

type SharedResources() = 
    static member ResourceManager = 
        new ResourceManager("blazorBookLibrary.Shared.Resources.SharedResources", typeof<SharedResources>.Assembly)

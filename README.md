# keepass
instant awesome

## Keepass.Background.Service

An attempt at building a .NET service that runs in the background, determines when the file has changed, and updating the related repository.
It also kicks off every 30 minutes automatically for pulls

Right now the main issue is that `Service` runs under it's own User, and this throws off Lib2Git. It doesn't like that the ownership of the `keepass` folder is ambiguous.
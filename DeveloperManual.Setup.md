# Developer manual: setting up the project:

This manual is meant to be used by developers to be capable of setting up the project within their machines:  

This is a C# led project, using godot as an easy to use UI engine, and it will target android, IOS and desktop, the project currently uses firebase, here is how to setup the project:  

# Important: Do not push APIKeys to the repository

# First dependency: FirebaseAdmin

Execute these commands within the project terminal, same folder the csproj is:  

```

dotnet add ChemClassify.csproj package FirebaseAdmin

```

Should work after that, some additional test if needed:  

```
dotnet --version

dotnet --list-sdks

Test-Path .\ChemClassify.csproj

Test-Path ".\APIKeys\<exact-filename>.json"  
```

Test-Path tests should return true after usage, if everything goes according to setup, now, execute these commands in order to setup credentials:

```
$credPath = (Resolve-Path ".\APIKeys\<exact-filename>.json").Path

$credPath
```

```
[System.Environment]::SetEnvironmentVariable(

  "GOOGLE_APPLICATION_CREDENTIALS",

$credPath,

"User"

)  
```

```
$env:GOOGLE_APPLICATION_CREDENTIALS = $credPath  
```

```
[System.Environment]::GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "User")

$env:GOOGLE_APPLICATION_CREDENTIALS

Test-Path $env:GOOGLE_APPLICATION_CREDENTIALS
```

you should see the credentials path after that, twice, as well as a True at the end for the Test-Path, reload your IDE and type this command to verify if eveyrthing worked:  

```
$env:GOOGLE_APPLICATION_CREDENTIALS
```

# Second Dependency: Simple mail transfer protocol

First, install MailKit via this command:  

```
dotnet add ChemClassify.csproj package MailKit
```

Then, execute these commands to set up the environment variables for the system to work

```
[System.Environment]::SetEnvironmentVariable("SMTP_HOST", "smtp.gmail.com", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_PORT", "587", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_USER", "<MailerInAPIKeys>", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_PASS", "<MailerInAPIKeys>", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_FROM", "<MailerInAPIKeys>", "User")
```

```
$env:SMTP_HOST = [System.Environment]::GetEnvironmentVariable("SMTP_HOST", "User")
$env:SMTP_PORT = [System.Environment]::GetEnvironmentVariable("SMTP_PORT", "User")
$env:SMTP_USER = [System.Environment]::GetEnvironmentVariable("SMTP_USER", "User")
$env:SMTP_PASS = [System.Environment]::GetEnvironmentVariable("SMTP_PASS", "User")
$env:SMTP_FROM = [System.Environment]::GetEnvironmentVariable("SMTP_FROM", "User")
```

```
$env:SMTP_HOST
$env:SMTP_PORT
$env:SMTP_USER
$env:SMTP_FROM
[string]::IsNullOrWhiteSpace($env:SMTP_PASS)
```

the last test should provide the environment variables plus a false, meaning that the password is not null or whitespace

# Success criteria in Godot

Before any godot testing is done, you must reload godot entirely afterall setups

the actual success criteria for the godot test: currently, the firebase test script is in a node within the main scene, when executing the project normally, you should see a sample UID from an user from the firebase auth section, as well as a message that says "Firebase admin connection seems ok"
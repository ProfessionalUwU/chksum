default:
    @just --list

project_name := `printf '%s\n' "${PWD##*/}"`
uppercase_project_name := capitalize(project_name)

setup:
    @mkdir src
    @dotnet new sln --name src/{{project_name}}
    @dotnet new classlib -o  src/{{uppercase_project_name}}
    @dotnet new xunit -o src/{{uppercase_project_name}}.Tests
    @dotnet sln add src/{{uppercase_project_name}}/{{uppercase_project_name}}.csproj
    @dotnet sln add src/{{uppercase_project_name}}.Tests/{{uppercase_project_name}}.Tests.csproj
    @dotnet add src/{{uppercase_project_name}}/{{uppercase_project_name}}.csproj reference src/{{uppercase_project_name}}.Tests/{{uppercase_project_name}}.Tests.csproj

run:
    @dotnet run

build:
    @dotnet build src/{{uppercase_project_name}}/{{uppercase_project_name}}.csproj
    @dotnet build src/{{uppercase_project_name}}.Tests/{{uppercase_project_name}}.Tests.csproj

publish: format
    @dotnet publish --configuration Release src/{{uppercase_project_name}}/{{uppercase_project_name}}.csproj

format:
    @dotnet format src/{{uppercase_project_name}}
    @dotnet format src/{{uppercase_project_name}}.Tests

test: build
    @dotnet test src/{{uppercase_project_name}}.Tests

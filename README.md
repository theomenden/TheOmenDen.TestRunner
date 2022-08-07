# The Omen Den's TestRunner
[![TheOmenDen.TestRunner](https://github-readme-stats.vercel.app/api/pin/?username=theomenden&repo=TheOmenDen.TestRunner&show_icons=true&theme=synthwave)](https://github.com/theomenden/THeOmenDen.TestRunner)
## The Goal of this project is to allow a user to run unit tests within their browser based off of various files that are uploaded.

## A list of accomplishments that we are aiming for with this project
1. Create a friendly user interface
   -  Allows users to readily see the tests that they are going to run
   -  Provide a simple indication for the total number of tests
   -  Provide an easy way to indicate between run and view tests that are in progress
   -  Provide a simple way to indicate test results
2.  Allows for tests to be run even when files are uploaded
    - Encourage the streaming result sets by allowing a "continuous run mode" which detects new tests and runs them automatically
    - Encourage the discrete result sets of the tests by having an option that allows for the user to indicate their preference for output, and result caching  
3.  Allows users to run tests from multiple frameworks simultaneously
    - XUnit
    - NUnit
    - BUnit
    - etc...  
  
## We are also working on creating support for a Progressive Web Application(PWA)
1. This is a long term solution to an offline problem that we will run into, especially if the client's application is disconnected from the current state.
2. This will also allow us to host this runner via a CDN or GitPages. 

## Working with the following libraries:
1. UI
   - [Blazorise](https://blazorise.com)
   - [Bootstrap 5](https://getbootstrap.com)
   - [Serilog](https://serilog.net/)
   - [The Omen Den's Shared Libraries](https://github.com/theomenden/TheOmenDen.Shared)
2. Frameworks
   - [Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor)
   - [.NET 6](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
   - [XUnit](https://xunit.net/)
   - [NUnit](https://nunit.org/)
  

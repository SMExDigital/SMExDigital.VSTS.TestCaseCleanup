# SMExDigital.VSTS.TestCaseCleanup

This project aims to identify and delete test cases in a VSTS Team Project that have no Test Steps
## Usage
```
SMExDigital.VSTS.TestCaseCleanup.exe -u https://<account>.visualstudio.com -p "My Team Project" -t <VSTS User Access Token> -d false
```

### Parameters
| Short Arg | Long Arg | Default | Description |
| ----------| ---------| --------| ----------- |
| -u | -uri | | Url to the TFS/VSTS account e.g. https://account.visualstudio.com |
| -p | -projectName | | Name of the team project to look for test cases in |
| -t | -token | | [Persoal Access Token](https://docs.microsoft.com/en-us/vsts/accounts/use-personal-access-tokens-to-authenticate) from VSTS |
| -d | -delete | false | Whether to delete the test cases with no test steps or not
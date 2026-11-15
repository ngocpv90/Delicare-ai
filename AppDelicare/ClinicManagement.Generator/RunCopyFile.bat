Print("----------AppDelicare.Data----------")
copy ..\ClinicManagement.Generator\ClinicManagement.Data.Connection\*.cs ..\AppDelicare.Data\Connection
copy ..\ClinicManagement.Generator\ClinicManagement.Data.Interface\*.cs ..\AppDelicare.Data\Interface
copy ..\ClinicManagement.Generator\ClinicManagement.Data.Repositories\*.cs ..\AppDelicare.Data\Repositories
copy ..\ClinicManagement.Generator\ClinicManagement.Data.Interface.UnitOfWork\*.cs ..\AppDelicare.Data\Interface
copy ..\ClinicManagement.Generator\ClinicManagement.Data.Repositories.UnitOfWork\*.cs ..\AppDelicare.Data\Repositories


Print("----------AppDelicare.Model----------")
copy ..\ClinicManagement.Generator\ClinicManagement.Model.Entity\*.cs ..\AppDelicare.Model\Entity
copy ..\ClinicManagement.Generator\ClinicManagement.Model.Request\*.cs ..\AppDelicare.Model\Request
for /r %%F in ("..\ClinicManagement.Generator\ClinicManagement.Model.SearchRequest\*.cs") do if not exist "..\AppDelicare.Model\SearchRequest\%%~nxF" copy %%F ..\AppDelicare.Model\SearchRequest
copy ..\ClinicManagement.Generator\ClinicManagement.Model.ViewModels\*.cs ..\AppDelicare.Model\ViewModels


Print("----------AppDelicare.Services----------")
for /r %%F in ("..\ClinicManagement.Generator\ClinicManagement.Services.Interface\*.cs") do if not exist "..\AppDelicare.Services\Interface\%%~nxF" copy %%F ..\AppDelicare.Services\Interface
for /r %%F in ("..\ClinicManagement.Generator\ClinicManagement.Services.Implement\*.cs") do if not exist "..\AppDelicare.Services\Implement\%%~nxF" copy %%F ..\AppDelicare.Services\Implement

Print("----------AppDelicare.WebApi----------")
for /r %%F in ("..\ClinicManagement.Generator\ClinicManagement.WebApi.Controllers\*.cs") do if not exist "..\AppDelicare.WebApi\Controllers\%%~nxF" copy %%F ..\AppDelicare.WebApi\Controllers
copy ..\ClinicManagement.Generator\ClinicManagement.WebApi.Infrastructure\*.cs ..\AppDelicare.WebApi\Infrastructure


Print("----------Delete All File Copy----------")
del "..\AppDelicare.Data\Connection\DomainGenerator.cs"
del "..\AppDelicare.Data\Interface\DomainGenerator.cs"
del "..\AppDelicare.Data\Repositories\DomainGenerator.cs"
del "..\AppDelicare.Model\Entity\DomainGenerator.cs"
del "..\AppDelicare.Model\Request\DomainGenerator.cs"
del "..\AppDelicare.Model\SearchRequest\DomainGenerator.cs"
del "..\AppDelicare.Model\ViewModels\DomainGenerator.cs"
del "..\AppDelicare.Services\Implement\DomainGenerator.cs"
del "..\AppDelicare.Services\Interface\DomainGenerator.cs"
del "..\AppDelicare.WebApi\Controllers\DomainGenerator.cs"
del "..\AppDelicare.WebApi\Infrastructure\DomainGenerator.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Data.Connection\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Data.Interface\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Data.Interface.UnitOfWork\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Data.Repositories\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Data.Repositories.UnitOfWork\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Model.Entity\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Model.Request\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Model.SearchRequest\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Model.ViewModels\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Services.Implement\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.Services.Interface\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.WebApi.Controllers\*.cs"
del "..\ClinicManagement.Generator\ClinicManagement.WebApi.Infrastructure\*.cs"
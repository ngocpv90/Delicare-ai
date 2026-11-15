Print("----------AppDelicare.Data----------")
copy ..\AppDelicare.Generator\AppDelicare.Data.Connection\*.cs ..\AppDelicare.Data\Connection
copy ..\AppDelicare.Generator\AppDelicare.Data.Interface\*.cs ..\AppDelicare.Data\Interface
copy ..\AppDelicare.Generator\AppDelicare.Data.Repositories\*.cs ..\AppDelicare.Data\Repositories
copy ..\AppDelicare.Generator\AppDelicare.Data.Interface.UnitOfWork\*.cs ..\AppDelicare.Data\Interface
copy ..\AppDelicare.Generator\AppDelicare.Data.Repositories.UnitOfWork\*.cs ..\AppDelicare.Data\Repositories


Print("----------AppDelicare.Model----------")
copy ..\AppDelicare.Generator\AppDelicare.Model.Entity\*.cs ..\AppDelicare.Model\Entity
copy ..\AppDelicare.Generator\AppDelicare.Model.Request\*.cs ..\AppDelicare.Model\Request
for /r %%F in ("..\AppDelicare.Generator\AppDelicare.Model.SearchRequest\*.cs") do if not exist "..\AppDelicare.Model\SearchRequest\%%~nxF" copy %%F ..\AppDelicare.Model\SearchRequest
copy ..\AppDelicare.Generator\AppDelicare.Model.ViewModels\*.cs ..\AppDelicare.Model\ViewModels


Print("----------AppDelicare.Services----------")
for /r %%F in ("..\AppDelicare.Generator\AppDelicare.Services.Interface\*.cs") do if not exist "..\AppDelicare.Services\Interface\%%~nxF" copy %%F ..\AppDelicare.Services\Interface
for /r %%F in ("..\AppDelicare.Generator\AppDelicare.Services.Implement\*.cs") do if not exist "..\AppDelicare.Services\Implement\%%~nxF" copy %%F ..\AppDelicare.Services\Implement

Print("----------AppDelicare.WebApi----------")
for /r %%F in ("..\AppDelicare.Generator\AppDelicare.WebApi.Controllers\*.cs") do if not exist "..\AppDelicare.WebApi\Controllers\%%~nxF" copy %%F ..\AppDelicare.WebApi\Controllers
copy ..\AppDelicare.Generator\AppDelicare.WebApi.Infrastructure\*.cs ..\AppDelicare.WebApi\Infrastructure


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
del "..\AppDelicare.Generator\AppDelicare.Data.Connection\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Data.Interface\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Data.Interface.UnitOfWork\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Data.Repositories\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Data.Repositories.UnitOfWork\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Model.Entity\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Model.Request\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Model.SearchRequest\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Model.ViewModels\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Services.Implement\*.cs"
del "..\AppDelicare.Generator\AppDelicare.Services.Interface\*.cs"
del "..\AppDelicare.Generator\AppDelicare.WebApi.Controllers\*.cs"
del "..\AppDelicare.Generator\AppDelicare.WebApi.Infrastructure\*.cs"
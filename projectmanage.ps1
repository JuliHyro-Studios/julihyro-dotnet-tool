[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ConfigPath = Join-Path $PSScriptRoot 'projectmanage_config.xml'
$script:default_config = @'
<?xml version="1.0" encoding="utf-8"?>
<ProjectManageConfig version="1">
  <Paths>
    <SourceDirectory>src</SourceDirectory>
    <SlnFile>dotnet-tool.sln</SlnFile>
    <SlnxFile>dotnet-tool.slnx</SlnxFile>
  </Paths>
  <Solution>
    <ProjectTypeGuid>{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuid>
    <VisualStudioVersion>17.0.31903.59</VisualStudioVersion>
    <MinimumVisualStudioVersion>10.0.40219.1</MinimumVisualStudioVersion>
    <SolutionFormatVersion>12.00</SolutionFormatVersion>
    <Platform>Any CPU</Platform>
    <DefaultConfigurations>
      <Configuration>Debug</Configuration>
      <Configuration>Release</Configuration>
    </DefaultConfigurations>
  </Solution>
  <Console>
    <Messages>
      <MainTitle>Project Manager</MainTitle>
      <SectionSeparator>==========================================================</SectionSeparator>
      <InvalidSelection>Invalid selection. Choose: {0}.</InvalidSelection>
      <NoProjects>No C# project files were found.</NoProjects>
      <ProjectListItem>[{0}] {1}</ProjectListItem>
      <MultipleProjectMatches>Multiple matching projects were found. Select the required project.</MultipleProjectMatches>
      <GenerateOne>1. Generate one solution file (choose SLN or SLNX)</GenerateOne>
      <GenerateBoth>2. Generate both SLN and SLNX</GenerateBoth>
      <FormatSln>1. SLN</FormatSln>
      <FormatSlnx>2. SLNX</FormatSlnx>
      <GenerationCompleted>Generation completed for {0} project(s).</GenerationCompleted>
      <NoCsproj>No .csproj files were found under {0}.</NoCsproj>
      <InsertSlnx>1. Insert into SLNX</InsertSlnx>
      <InsertSln>2. Insert into SLN</InsertSln>
      <AlreadyReferenced>Every project under {0} is already referenced by {1}.</AlreadyReferenced>
      <ProjectsWillInsert>The following projects will be inserted:</ProjectsWillInsert>
      <ProjectInsertItem>  + {0}</ProjectInsertItem>
      <NoChangesMade>No changes were made.</NoChangesMade>
      <InsertCompleted>Successfully inserted {0} project(s) into {1}.</InsertCompleted>
      <SolutionNotFoundSkip>{0} was not found. Skipping it.</SolutionNotFoundSkip>
      <UnavailableCount>{0}: {1} unavailable project reference(s).</UnavailableCount>
      <UnavailableItem>  - {0} [{1}]</UnavailableItem>
      <NoUnavailable>No unavailable project references were found.</NoUnavailable>
      <RemoveSlnx>1. Remove unavailable references from SLNX</RemoveSlnx>
      <RemoveSln>2. Remove unavailable references from SLN</RemoveSln>
      <RemoveBoth>3. Remove unavailable references from both</RemoveBoth>
      <RepairSummary>{0} will retain {1} project reference(s) and remove {2}.</RepairSummary>
      <Repaired>{0} was repaired successfully.</Repaired>
      <SkippedRepair>Skipped {0}. No changes were made to this file.</SkippedRepair>
      <CheckSlnx>1. Check and repair SLNX</CheckSlnx>
      <CheckSln>2. Check and repair SLN</CheckSln>
      <CheckBoth>3. Check and repair both</CheckBoth>
      <SolutionDoesNotExist>{0} does not exist. Skipping it.</SolutionDoesNotExist>
      <ProjectOk>[OK] {0} -&gt; {1}</ProjectOk>
      <MissingProjectPath>Missing project path: {0}</MissingProjectPath>
      <ClosestMatches>Closest project matches found under {0}:</ClosestMatches>
      <CandidateItem>[{0}] {1} | {2} | distance: {3}</CandidateItem>
      <SkipReference>[0] Skip this reference</SkipReference>
      <Unchanged>This reference was left unchanged.</Unchanged>
      <InvalidUnchanged>Invalid selection. This reference was left unchanged.</InvalidUnchanged>
      <ReplacementSelected>Replacement selected: {0}</ReplacementSelected>
      <NoPathChanges>No path changes are required for {0}.</NoPathChanges>
      <PathsUpdated>{0} paths were updated successfully.</PathsUpdated>
      <ChangesDiscarded>Changes to {0} were discarded.</ChangesDiscarded>
      <FixUnavailable>1. Fix unavailable project references</FixUnavailable>
      <FixLocations>2. Fix project location paths</FixLocations>
      <MainInsert>1. Insert New Project from {0}</MainInsert>
      <MainFix>2. Fix Project Tool</MainFix>
      <MainGenerate>3. Generate New SLN / SLNX</MainGenerate>
      <MainExit>0. Exit</MainExit>
      <Closed>Project Manager closed.</Closed>
      <OperationFailed>Operation failed: {0}</OperationFailed>
      <NoFurtherChanges>No further changes will be attempted in this operation.</NoFurtherChanges>
      <Blank/>
      <SectionTitleLine>  {0}</SectionTitleLine>
      <GenerateSection>Generate New Solution Files</GenerateSection>
      <InsertSection>Insert New Projects from {0}</InsertSection>
      <FixUnavailableSection>Fix Unavailable Project References</FixUnavailableSection>
      <FixLocationsSection>Fix Project Location Paths</FixLocationsSection>
      <FixToolSection>Fix Project Tool</FixToolSection>
    </Messages>
    <Colors>
      <SectionSeparator>DarkCyan</SectionSeparator>
      <SectionTitle>Cyan</SectionTitle>
      <InvalidSelection>Yellow</InvalidSelection>
      <NoProjects>Yellow</NoProjects>
      <GenerationCompleted>Green</GenerationCompleted>
      <NoCsproj>Yellow</NoCsproj>
      <AlreadyReferenced>Green</AlreadyReferenced>
      <InsertCompleted>Green</InsertCompleted>
      <SolutionNotFoundSkip>DarkYellow</SolutionNotFoundSkip>
      <UnavailableItem>Yellow</UnavailableItem>
      <NoUnavailable>Green</NoUnavailable>
      <Repaired>Green</Repaired>
      <SolutionDoesNotExist>DarkYellow</SolutionDoesNotExist>
      <ProjectOk>Green</ProjectOk>
      <MissingProjectPath>Yellow</MissingProjectPath>
      <Unchanged>DarkYellow</Unchanged>
      <InvalidUnchanged>Yellow</InvalidUnchanged>
      <ReplacementSelected>Green</ReplacementSelected>
      <PathsUpdated>Green</PathsUpdated>
      <OperationFailed>Red</OperationFailed>
      <NoFurtherChanges>Yellow</NoFurtherChanges>
      <SectionTitleLine>Cyan</SectionTitleLine>
    </Colors>
    <Prompts>
      <ChooseOperation>Choose an operation</ChooseOperation>
      <SelectProjectMode>Select 1 for a single project or 2 for multiple projects</SelectProjectMode>
      <ProjectNumber>Enter the project number</ProjectNumber>
      <ProjectNumbers>Enter project numbers separated by commas (example: 1,3,4)</ProjectNumbers>
      <ProjectName>Enter a project name or .csproj file name</ProjectName>
      <GenerationMode>Choose a generation mode</GenerationMode>
      <SelectProjectInput>Select 1 to scan and select project folders, or 2 to enter a project name</SelectProjectInput>
      <SolutionFormat>Choose the solution format</SolutionFormat>
      <TargetSolution>Choose the target solution</TargetSolution>
      <ConfirmInsert>Insert {0} project(s) into {1}? 1=Yes, 2=No</ConfirmInsert>
      <RepairTarget>Choose which solution file(s) to repair</RepairTarget>
      <ConfirmRepair>Confirm changes to {0}? 1=Yes, 2=No</ConfirmRepair>
      <InspectTarget>Choose the solution file(s) to inspect</InspectTarget>
      <ReplacementSelection>Select a replacement for '{0}'</ReplacementSelection>
      <RepairOperation>Choose a repair operation</RepairOperation>
      <SaveRepairedPaths>Save the repaired paths to {0}? 1=Yes, 2=No</SaveRepairedPaths>
      <ReturnToMenu>Press Enter to return to the main menu</ReturnToMenu>
    </Prompts>
  </Console>
</ProjectManageConfig>
'@

if (-not (Test-Path -LiteralPath $script:ConfigPath -PathType Leaf)) {
    [IO.File]::WriteAllText($script:ConfigPath, $script:default_config, (New-Object System.Text.UTF8Encoding($true)))
}

try {
    [xml]$script:ConfigDocument = Get-Content -LiteralPath $script:ConfigPath -Raw -ErrorAction Stop
    if ($script:ConfigDocument.DocumentElement.Name -ne 'ProjectManageConfig' -or [string]$script:ConfigDocument.ProjectManageConfig.version -ne '1') {
        throw 'Invalid or unsupported configuration document.'
    }
}
catch {
    throw "Unable to load projectmanage_config.xml: $($_.Exception.Message)"
}

# Keep the XmlDocument and its root XmlElement in separate variables.
# A variable declared with [xml] cannot later accept an XmlElement.
$script:Config = $script:ConfigDocument.ProjectManageConfig
$script:RootPath = [IO.Path]::GetFullPath($PSScriptRoot)
$script:SourcePath = Join-Path $script:RootPath ([string]$script:Config.Paths.SourceDirectory)
$script:SlnPath = Join-Path $script:RootPath ([string]$script:Config.Paths.SlnFile)
$script:SlnxPath = Join-Path $script:RootPath ([string]$script:Config.Paths.SlnxFile)
$script:SlnProjectTypeGuid = [string]$script:Config.Solution.ProjectTypeGuid

function Get-ConfigValue {
    param([Parameter(Mandatory)][string]$Section, [Parameter(Mandatory)][string]$Key)
    $node = $script:Config.SelectSingleNode("$Section/$Key")
    if ($null -eq $node) { throw "Missing configuration value: $Section/$Key" }
    return [string]$node.InnerText
}

function Format-ConfigValue {
    param([Parameter(Mandatory)][string]$Section, [Parameter(Mandatory)][string]$Key, [object[]]$Values = @())
    $template = Get-ConfigValue -Section $Section -Key $Key
    if ($Values.Count -gt 0) { return ($template -f $Values) }
    return $template
}

function Write-ConfigHost {
    param([Parameter(Mandatory)][string]$Key, [object[]]$Values = @(), [switch]$NoNewline)
    $message = Format-ConfigValue -Section 'Console/Messages' -Key $Key -Values $Values
    $colorNode = $script:Config.SelectSingleNode("Console/Colors/$Key")
    if ($null -ne $colorNode -and -not [string]::IsNullOrWhiteSpace([string]$colorNode.InnerText)) {
        if ($NoNewline) { Write-Host $message -ForegroundColor ([string]$colorNode.InnerText) -NoNewline }
        else { Write-Host $message -ForegroundColor ([string]$colorNode.InnerText) }
    }
    else {
        if ($NoNewline) { Write-Host $message -NoNewline }
        else { Write-Host $message }
    }
}

function Read-ConfigHost {
    param([Parameter(Mandatory)][string]$Key, [object[]]$Values = @())
    return Read-Host (Format-ConfigValue -Section 'Console/Prompts' -Key $Key -Values $Values)
}

function Write-Section {
    param([Parameter(Mandatory)][string]$Title)
    Write-ConfigHost -Key 'Blank'
    Write-ConfigHost -Key 'SectionSeparator'
    Write-ConfigHost -Key 'SectionTitleLine' -Values @($Title)
    Write-ConfigHost -Key 'SectionSeparator'
}

function Read-MenuChoice {
    param(
        [Parameter(Mandatory)][string]$Prompt,
        [Parameter(Mandatory)][string[]]$Allowed
    )
    while ($true) {
        $answer = (Read-Host $Prompt).Trim()
        if ($Allowed -contains $answer) { return $answer }
        Write-ConfigHost -Key 'InvalidSelection' -Values @($Allowed -join ', ')
    }
}

function Get-SourceProjects {
    if (-not (Test-Path -LiteralPath $script:SourcePath -PathType Container)) {
        throw "The source directory was not found: $script:SourcePath"
    }

    $projects = @(
        Get-ChildItem -LiteralPath $script:SourcePath -Filter '*.csproj' -File -Recurse |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
                $_.FullName -notmatch '[\\/]\.git[\\/]'
            } |
            Sort-Object FullName
    )
    return $projects
}

function ConvertTo-RootRelativePath {
    param([Parameter(Mandatory)][string]$FullPath)

    $full = [IO.Path]::GetFullPath($FullPath)
    $rootUri = [Uri]($script:RootPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar)
    $fileUri = [Uri]$full
    $relative = [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fileUri).ToString())
    return $relative.Replace('\', '/')
}

function ConvertTo-AbsoluteProjectPath {
    param([Parameter(Mandatory)][string]$RelativePath)

    $normalized = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    return [IO.Path]::GetFullPath((Join-Path $script:RootPath $normalized))
}

function Get-ProjectNameFromPath {
    param([Parameter(Mandatory)][string]$Path)
    return [IO.Path]::GetFileNameWithoutExtension($Path)
}

function Get-SolutionFilePath {
    param([ValidateSet('SLN', 'SLNX')][string]$Format)
    if ($Format -eq 'SLN') { return $script:SlnPath }
    return $script:SlnxPath
}

function Get-SolutionProjects {
    param([ValidateSet('SLN', 'SLNX')][string]$Format)

    $filePath = Get-SolutionFilePath $Format
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        return @()
    }

    $result = @()
    if ($Format -eq 'SLNX') {
        [xml]$document = Get-Content -LiteralPath $filePath -Raw
        foreach ($node in $document.SelectNodes('/Solution/Project[@Path]')) {
            $projectPath = [string]$node.GetAttribute('Path')
            if ([string]::IsNullOrWhiteSpace($projectPath)) { continue }
            $result += [pscustomobject]@{
                Name = (Get-ProjectNameFromPath -Path $projectPath)
                Path = $projectPath.Replace('\', '/')
                Guid = ''
                TypeGuid = ''
            }
        }
    }
    else {
        foreach ($line in (Get-Content -LiteralPath $filePath)) {
            if ($line -match '^Project\("\{(?<TypeGuid>[^}]+)\}"\)\s*=\s*"(?<Name>[^"]+)",\s*"(?<Path>[^"]+)",\s*"\{(?<Guid>[^}]+)\}"') {
                $result += [pscustomobject]@{
                    Name = $Matches['Name']
                    Path = $Matches['Path'].Replace('\', '/')
                    Guid = ('{' + $Matches['Guid'].ToUpperInvariant() + '}')
                    TypeGuid = ('{' + $Matches['TypeGuid'].ToUpperInvariant() + '}')
                }
            }
        }
    }
    return @($result)
}

function Get-DeterministicProjectGuid {
    param([Parameter(Mandatory)][string]$RelativePath)

    $bytes = [Text.Encoding]::UTF8.GetBytes($RelativePath.Replace('\', '/').ToLowerInvariant())
    $md5 = [Security.Cryptography.MD5]::Create()
    try { $hash = $md5.ComputeHash($bytes) }
    finally { $md5.Dispose() }

    # Set UUID version 3 and RFC 4122 variant bits.
    $hash[6] = ($hash[6] -band 0x0F) -bor 0x30
    $hash[8] = ($hash[8] -band 0x3F) -bor 0x80
    $guid = New-Object Guid (,$hash[0..15])
    return '{' + $guid.ToString().ToUpperInvariant() + '}'
}

function Save-Slnx {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Projects)

    $document = New-Object System.Xml.XmlDocument
    $document.AppendChild($document.CreateXmlDeclaration('1.0', 'utf-8', $null)) | Out-Null
    $solutionNode = $document.CreateElement('Solution')
    [void]$document.AppendChild($solutionNode)

    foreach ($project in $Projects) {
        $node = $document.CreateElement('Project')
        $node.SetAttribute('Path', ([string]$project.Path).Replace('\', '/'))
        [void]$solutionNode.AppendChild($node)
    }

    $temporaryPath = Join-Path $script:RootPath ([IO.Path]::GetRandomFileName() + '.tmp')
    try {
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Encoding = New-Object System.Text.UTF8Encoding($true)
        $settings.Indent = $true
        $settings.IndentChars = '  '
        $writer = [Xml.XmlWriter]::Create($temporaryPath, $settings)
        try { $document.Save($writer) }
        finally { $writer.Dispose() }

        # Validate the generated XML before replacing the existing solution.
        [xml]$validationDocument = Get-Content -LiteralPath $temporaryPath -Raw
        if ($validationDocument.DocumentElement.Name -ne 'Solution') {
            throw 'Generated SLNX validation failed: the root element is not Solution.'
        }
        Move-Item -LiteralPath $temporaryPath -Destination $script:SlnxPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Save-Sln {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Projects)

    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add(('Microsoft Visual Studio Solution File, Format Version {0}' -f $script:Config.Solution.SolutionFormatVersion))
    $lines.Add(('# Visual Studio Version {0}' -f ([string]$script:Config.Solution.VisualStudioVersion -split '\.')[0]))
    $lines.Add(('VisualStudioVersion = {0}' -f $script:Config.Solution.VisualStudioVersion))
    $lines.Add(('MinimumVisualStudioVersion = {0}' -f $script:Config.Solution.MinimumVisualStudioVersion))

    $projectGuids = @{}
    foreach ($project in $Projects) {
        $relativePath = ([string]$project.Path).Replace('/', '\')
        $guid = [string]$project.Guid
        if ([string]::IsNullOrWhiteSpace($guid)) {
            $guid = Get-DeterministicProjectGuid ([string]$project.Path)
        }
        if ($guid -notmatch '^\{[0-9A-Fa-f-]{36}\}$') {
            throw "Invalid project GUID for '$relativePath': $guid"
        }
        $projectTypeGuid = [string]$project.TypeGuid
        if ([string]::IsNullOrWhiteSpace($projectTypeGuid)) {
            $projectTypeGuid = $script:SlnProjectTypeGuid
        }
        $name = [string]$project.Name
        $lines.Add(('Project("{0}") = "{1}", "{2}", "{3}"' -f $projectTypeGuid, $name, $relativePath, $guid))
        $lines.Add('EndProject')
        $projectGuids[$guid.ToUpperInvariant()] = $true
    }

    $lines.Add('Global')
    $lines.Add("`tGlobalSection(SolutionConfigurationPlatforms) = preSolution")
    foreach ($configurationNode in $script:Config.Solution.DefaultConfigurations.Configuration) {
        $configuration = [string]$configurationNode
        $platform = [string]$script:Config.Solution.Platform
        $lines.Add(("`t`t{0}|{1} = {0}|{1}" -f $configuration, $platform))
    }
    $lines.Add("`tEndGlobalSection")
    $lines.Add("`tGlobalSection(ProjectConfigurationPlatforms) = postSolution")

    foreach ($guid in ($projectGuids.Keys | Sort-Object)) {
        foreach ($configurationNode in $script:Config.Solution.DefaultConfigurations.Configuration) {
            $configuration = [string]$configurationNode
            $platform = [string]$script:Config.Solution.Platform
            $lines.Add("`t`t$guid.$configuration|$platform.ActiveCfg = $configuration|$platform")
            $lines.Add("`t`t$guid.$configuration|$platform.Build.0 = $configuration|$platform")
        }
    }

    $lines.Add("`tEndGlobalSection")
    $lines.Add('EndGlobal')

    $temporaryPath = Join-Path $script:RootPath ([IO.Path]::GetRandomFileName() + '.tmp')
    try {
        $encoding = New-Object System.Text.UTF8Encoding($true)
        [IO.File]::WriteAllLines($temporaryPath, $lines, $encoding)

        # Validate project declarations and paths before replacing the solution.
        $validationProjects = @(
            Get-Content -LiteralPath $temporaryPath |
                Where-Object { $_ -match '^Project\("\{' }
        )
        if ($validationProjects.Count -ne $Projects.Count) {
            throw 'Generated SLN validation failed: the project count does not match.'
        }
        Move-Item -LiteralPath $temporaryPath -Destination $script:SlnPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Save-Solution {
    param(
        [Parameter(Mandatory)][ValidateSet('SLN', 'SLNX')][string]$Format,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Projects
    )

    $uniqueProjects = @()
    $seen = @{}
    foreach ($project in $Projects) {
        $normalizedPath = ([string]$project.Path).Replace('\', '/')
        $key = $normalizedPath.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $uniqueProjects += [pscustomobject]@{
            Name = [string]$project.Name
            Path = $normalizedPath
            Guid = [string]$project.Guid
            TypeGuid = [string]$project.TypeGuid
        }
    }

    if ($Format -eq 'SLN') { Save-Sln -Projects $uniqueProjects }
    else { Save-Slnx -Projects $uniqueProjects }
}

function Select-ProjectItems {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Projects)

    if ($Projects.Count -eq 0) {
        Write-ConfigHost -Key 'NoProjects'
        return @()
    }

    Write-ConfigHost -Key 'Blank'
    for ($index = 0; $index -lt $Projects.Count; $index++) {
        Write-ConfigHost -Key 'ProjectListItem' -Values @(($index + 1), (ConvertTo-RootRelativePath $Projects[$index].FullName))
    }

    $selectionMode = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'SelectProjectMode') @('1', '2')
    if ($selectionMode -eq '1') {
        $number = Read-ConfigHost -Key 'ProjectNumber'
        if ($number -notmatch '^\d+$' -or [int]$number -lt 1 -or [int]$number -gt $Projects.Count) {
            throw 'The selected project number is out of range.'
        }
        return @($Projects[[int]$number - 1])
    }

    $rawSelection = Read-ConfigHost -Key 'ProjectNumbers'
    $selected = @()
    foreach ($part in ($rawSelection -split '[,; ]+' | Where-Object { $_ })) {
        if ($part -notmatch '^\d+$' -or [int]$part -lt 1 -or [int]$part -gt $Projects.Count) {
            throw "Invalid project number: $part"
        }
        $selected += $Projects[[int]$part - 1]
    }
    if ($selected.Count -eq 0) { throw 'No projects were selected.' }
    return @($selected | Sort-Object FullName -Unique)
}

function Resolve-ProjectsForGeneration {
    param([Parameter(Mandatory)][object[]]$AllProjects)

    $inputMode = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'SelectProjectInput') @('1', '2')
    if ($inputMode -eq '1') {
        return @(Select-ProjectItems -Projects $AllProjects)
    }

    $projectName = (Read-ConfigHost -Key 'ProjectName').Trim()
    if ([string]::IsNullOrWhiteSpace($projectName)) { throw 'The project name cannot be empty.' }

    $matches = @(
        $AllProjects | Where-Object {
            $_.BaseName -ieq $projectName -or
            $_.Name -ieq $projectName -or
            $_.Directory.Name -ieq $projectName
        }
    )
    if ($matches.Count -eq 0) { throw "No project named '$projectName' was found under src." }
    if ($matches.Count -eq 1) { return $matches }
    Write-ConfigHost -Key 'MultipleProjectMatches'
    return @(Select-ProjectItems -Projects $matches)
}

function Invoke-GenerateSolutions {
    Write-Section (Get-ConfigValue -Section 'Console/Messages' -Key 'GenerateSection')
    Write-ConfigHost -Key 'GenerateOne'
    Write-ConfigHost -Key 'GenerateBoth'
    $generationMode = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'GenerationMode') @('1', '2')

    $allProjects = @(Get-SourceProjects)
    $selectedProjects = @(Resolve-ProjectsForGeneration -AllProjects $allProjects)
    if ($selectedProjects.Count -eq 0) { return }

    $entries = @(
        $selectedProjects | ForEach-Object {
            [pscustomobject]@{
                Name = $_.BaseName
                Path = ConvertTo-RootRelativePath $_.FullName
                Guid = ''
                TypeGuid = ''
            }
        }
    )

    if ($generationMode -eq '1') {
        Write-ConfigHost -Key 'FormatSln'
        Write-ConfigHost -Key 'FormatSlnx'
        $formatChoice = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'SolutionFormat') @('1', '2')
        if ($formatChoice -eq '1') { Save-Solution -Format 'SLN' -Projects $entries }
        else { Save-Solution -Format 'SLNX' -Projects $entries }
    }
    else {
        Save-Solution -Format 'SLN' -Projects $entries
        Save-Solution -Format 'SLNX' -Projects $entries
    }

    Write-ConfigHost -Key 'GenerationCompleted' -Values @($entries.Count)
}

function Invoke-InsertProjects {
    Write-Section (Format-ConfigValue -Section 'Console/Messages' -Key 'InsertSection' -Values @($script:Config.Paths.SourceDirectory))
    $projects = @(Get-SourceProjects)
    if ($projects.Count -eq 0) {
        Write-ConfigHost -Key 'NoCsproj' -Values @($script:Config.Paths.SourceDirectory)
        return
    }

    Write-ConfigHost -Key 'InsertSlnx'
    Write-ConfigHost -Key 'InsertSln'
    $choice = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'TargetSolution') @('1', '2')
    $format = if ($choice -eq '1') { 'SLNX' } else { 'SLN' }

    $existing = @(Get-SolutionProjects -Format $format)
    $knownPaths = @{}
    foreach ($entry in $existing) {
        $knownPaths[$entry.Path.Replace('\', '/').ToLowerInvariant()] = $true
    }

    $newProjects = @(
        $projects | Where-Object {
            $relative = (ConvertTo-RootRelativePath $_.FullName).ToLowerInvariant()
            -not $knownPaths.ContainsKey($relative)
        }
    )

    if ($newProjects.Count -eq 0) {
        Write-ConfigHost -Key 'AlreadyReferenced' -Values @($script:Config.Paths.SourceDirectory, $format)
        return
    }

    Write-ConfigHost -Key 'ProjectsWillInsert'
    foreach ($project in $newProjects) {
        Write-ConfigHost -Key 'ProjectInsertItem' -Values @((ConvertTo-RootRelativePath $project.FullName))
    }

    $entries = @($existing)
    foreach ($project in $newProjects) {
        $entries += [pscustomobject]@{
            Name = $project.BaseName
            Path = ConvertTo-RootRelativePath $project.FullName
            Guid = ''
            TypeGuid = ''
        }
    }

    $confirm = Read-MenuChoice (Format-ConfigValue -Section 'Console/Prompts' -Key 'ConfirmInsert' -Values @($newProjects.Count, $format)) @('1', '2')
    if ($confirm -eq '2') {
        Write-ConfigHost -Key 'NoChangesMade'
        return
    }

    Save-Solution -Format $format -Projects $entries
    Write-ConfigHost -Key 'InsertCompleted' -Values @($newProjects.Count, $format)
}

function Test-SolutionProjectPath {
    param([Parameter(Mandatory)][string]$RelativePath)

    try {
        $absolutePath = ConvertTo-AbsoluteProjectPath $RelativePath
        return (Test-Path -LiteralPath $absolutePath -PathType Leaf)
    }
    catch {
        return $false
    }
}

function Invoke-FixUnavailableProjects {
    Write-Section (Get-ConfigValue -Section 'Console/Messages' -Key 'FixUnavailableSection')
    $missingByFormat = @{}

    foreach ($format in @('SLNX', 'SLN')) {
        $filePath = Get-SolutionFilePath $format
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            Write-ConfigHost -Key 'SolutionNotFoundSkip' -Values @($format)
            $missingByFormat[$format] = @()
            continue
        }

        $missing = @(
            Get-SolutionProjects -Format $format |
                Where-Object { -not (Test-SolutionProjectPath -RelativePath $_.Path) }
        )
        $missingByFormat[$format] = $missing
        Write-ConfigHost -Key 'UnavailableCount' -Values @($format, $missing.Count)
        foreach ($project in $missing) {
            Write-ConfigHost -Key 'UnavailableItem' -Values @($project.Name, $project.Path)
        }
    }

    if ($missingByFormat['SLNX'].Count -eq 0 -and $missingByFormat['SLN'].Count -eq 0) {
        Write-ConfigHost -Key 'NoUnavailable'
        return
    }

    Write-ConfigHost -Key 'RemoveSlnx'
    Write-ConfigHost -Key 'RemoveSln'
    Write-ConfigHost -Key 'RemoveBoth'
    $target = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'RepairTarget') @('1', '2', '3')

    $formats = @()
    if ($target -eq '1') { $formats = @('SLNX') }
    elseif ($target -eq '2') { $formats = @('SLN') }
    else { $formats = @('SLNX', 'SLN') }

    foreach ($format in $formats) {
        $filePath = Get-SolutionFilePath $format
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { continue }

        $unavailablePaths = @{}
        foreach ($project in $missingByFormat[$format]) {
            $unavailablePaths[$project.Path.Replace('\', '/').ToLowerInvariant()] = $true
        }

        $remaining = @(
            Get-SolutionProjects -Format $format |
                Where-Object { -not $unavailablePaths.ContainsKey($_.Path.Replace('\', '/').ToLowerInvariant()) }
        )

        Write-ConfigHost -Key 'RepairSummary' -Values @($format, $remaining.Count, $missingByFormat[$format].Count) 
        $confirmation = Read-MenuChoice (Format-ConfigValue -Section 'Console/Prompts' -Key 'ConfirmRepair' -Values @($format)) @('1', '2')
        if ($confirmation -eq '1') {
            Save-Solution -Format $format -Projects $remaining
            Write-ConfigHost -Key 'Repaired' -Values @($format)
        }
        else {
            Write-ConfigHost -Key 'SkippedRepair' -Values @($format)
        }
    }
}

function Get-LevenshteinDistance {
    param(
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right
    )

    $a = $Left.ToLowerInvariant()
    $b = $Right.ToLowerInvariant()
    $matrix = New-Object 'int[,]' ($a.Length + 1), ($b.Length + 1)

    for ($i = 0; $i -le $a.Length; $i++) { $matrix[$i, 0] = $i }
    for ($j = 0; $j -le $b.Length; $j++) { $matrix[0, $j] = $j }

    for ($i = 1; $i -le $a.Length; $i++) {
        for ($j = 1; $j -le $b.Length; $j++) {
            $cost = 1
            if ($a[$i - 1] -eq $b[$j - 1]) { $cost = 0 }
            $deletion = $matrix[$i - 1, $j] + 1
            $insertion = $matrix[$i, $j - 1] + 1
            $substitution = $matrix[$i - 1, $j - 1] + $cost
            $matrix[$i, $j] = [Math]::Min([Math]::Min($deletion, $insertion), $substitution)
        }
    }

    return $matrix[$a.Length, $b.Length]
}

function Invoke-FixProjectLocation {
    Write-Section (Get-ConfigValue -Section 'Console/Messages' -Key 'FixLocationsSection')
    $allProjects = @(Get-SourceProjects)
    if ($allProjects.Count -eq 0) {
        Write-ConfigHost -Key 'NoCsproj' -Values @($script:Config.Paths.SourceDirectory)
        return
    }

    Write-ConfigHost -Key 'CheckSlnx'
    Write-ConfigHost -Key 'CheckSln'
    Write-ConfigHost -Key 'CheckBoth'
    $target = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'InspectTarget') @('1', '2', '3')

    $formats = @()
    if ($target -eq '1') { $formats = @('SLNX') }
    elseif ($target -eq '2') { $formats = @('SLN') }
    else { $formats = @('SLNX', 'SLN') }

    foreach ($format in $formats) {
        $solutionPath = Get-SolutionFilePath $format
        if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
            Write-ConfigHost -Key 'SolutionDoesNotExist' -Values @($format)
            continue
        }

        $entries = @(Get-SolutionProjects -Format $format)
        $changed = $false

        for ($index = 0; $index -lt $entries.Count; $index++) {
            $entry = $entries[$index]
            if (Test-SolutionProjectPath -RelativePath $entry.Path) {
                Write-ConfigHost -Key 'ProjectOk' -Values @($entry.Name, $entry.Path)
                continue
            }

            Write-ConfigHost -Key 'Blank'
            Write-ConfigHost -Key 'MissingProjectPath' -Values @($entry.Path)
            $ranked = @(
                $allProjects | ForEach-Object {
                    [pscustomobject]@{
                        Project = $_
                        Distance = Get-LevenshteinDistance -Left $entry.Name -Right $_.BaseName
                    }
                } | Sort-Object Distance, { $_.Project.FullName }
            )

            $limit = [Math]::Min(5, $ranked.Count)
            Write-ConfigHost -Key 'ClosestMatches' -Values @($script:Config.Paths.SourceDirectory)
            for ($candidateIndex = 0; $candidateIndex -lt $limit; $candidateIndex++) {
                $candidate = $ranked[$candidateIndex]
                Write-ConfigHost -Key 'CandidateItem' -Values @(($candidateIndex + 1), $candidate.Project.BaseName, (ConvertTo-RootRelativePath $candidate.Project.FullName), $candidate.Distance)
            }
            Write-ConfigHost -Key 'SkipReference'

            $selection = Read-ConfigHost -Key 'ReplacementSelection' -Values @($entry.Name)
            if ($selection -notmatch '^\d+$' -or [int]$selection -eq 0) {
                Write-ConfigHost -Key 'Unchanged'
                continue
            }
            if ([int]$selection -lt 1 -or [int]$selection -gt $limit) {
                Write-ConfigHost -Key 'InvalidUnchanged'
                continue
            }

            $replacement = $ranked[[int]$selection - 1].Project
            $replacementPath = ConvertTo-RootRelativePath $replacement.FullName
            $entries[$index] = [pscustomobject]@{
                Name = $replacement.BaseName
                Path = $replacementPath
                Guid = $entry.Guid
                TypeGuid = $entry.TypeGuid
            }
            $changed = $true
            Write-ConfigHost -Key 'ReplacementSelected' -Values @($replacementPath)
        }

        if (-not $changed) {
            Write-ConfigHost -Key 'NoPathChanges' -Values @($format)
            continue
        }

        $confirmation = Read-MenuChoice (Format-ConfigValue -Section 'Console/Prompts' -Key 'SaveRepairedPaths' -Values @($format)) @('1', '2')
        if ($confirmation -eq '1') {
            Save-Solution -Format $format -Projects $entries
            Write-ConfigHost -Key 'PathsUpdated' -Values @($format)
        }
        else {
            Write-ConfigHost -Key 'ChangesDiscarded' -Values @($format)
        }
    }
}

function Invoke-FixProjectTool {
    Write-Section (Get-ConfigValue -Section 'Console/Messages' -Key 'FixToolSection')
    Write-ConfigHost -Key 'FixUnavailable'
    Write-ConfigHost -Key 'FixLocations'
    $choice = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'RepairOperation') @('1', '2')

    if ($choice -eq '1') { Invoke-FixUnavailableProjects }
    else { Invoke-FixProjectLocation }
}

function Show-MainMenu {
    Write-Section (Get-ConfigValue -Section 'Console/Messages' -Key 'MainTitle')
    Write-ConfigHost -Key 'MainInsert' -Values @($script:Config.Paths.SourceDirectory)
    Write-ConfigHost -Key 'MainFix'
    Write-ConfigHost -Key 'MainGenerate'
    Write-ConfigHost -Key 'MainExit'
}

while ($true) {
    try {
        Show-MainMenu
        $choice = Read-MenuChoice (Get-ConfigValue -Section 'Console/Prompts' -Key 'ChooseOperation') @('0', '1', '2', '3')

        switch ($choice) {
            '1' { Invoke-InsertProjects }
            '2' { Invoke-FixProjectTool }
            '3' { Invoke-GenerateSolutions }
            '0' {
                Write-ConfigHost -Key 'Closed'
                break
            }
        }

        if ($choice -eq '0') { break }
    }
    catch {
        Write-ConfigHost -Key 'Blank'
        Write-ConfigHost -Key 'OperationFailed' -Values @($_.Exception.Message)
        Write-ConfigHost -Key 'NoFurtherChanges'
    }

    [void](Read-ConfigHost -Key 'ReturnToMenu')
}

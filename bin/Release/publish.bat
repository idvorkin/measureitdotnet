mkdir stage_nuget\lib\net20
copy MeasureIt.exe stage_nuget\lib\net20
copy MeasureIt.exe.config stage_nuget\lib\net20
copy ..\..\MeasureIt.exe.nuspec stage_nuget
pushd stage_nuget
nuget pack MeasureIt.exe.nuspec
popd


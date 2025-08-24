using Unity.VisualScripting;
using Zenject;

public class GameInstaller : MonoInstaller
{
	public override void InstallBindings()
	{
		// Bind the provider by searching the scene for a component that implements it.
		Container.Bind<ITeamBaseProvider>().To<TeamBaseProvider>().FromComponentInHierarchy().AsSingle();
	}
}
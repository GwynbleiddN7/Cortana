using System.Net.Sockets;
using System.Text;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Renci.SshNet;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware;

internal static class ComputerService
{
	private static Socket? _computerClient;

	static ComputerService()
	{
		_ = new Timer("", null, (0, 1, 0), CheckConnection, ETimerType.Utility, ETimerLoop.Interval);
	}
	
	public static void BindClient(Socket handler)
	{
		_computerClient?.Close();
		_computerClient = handler;
		UpdateComputerStatus(EPower.On);
		Task.Run(Read);
	}

	private static void Read()
	{
		if(ClientUnavailable()) return;
		
		try
		{
			while (true)
			{
				var buffer = new byte[1_024];
				int received = _computerClient!.Receive(buffer);
				string message = Encoding.UTF8.GetString(buffer, 0, received);
				if (received == 0) continue;
				
				switch (message)
				{
					case "poweroff" or "reboot":
						Task.Delay(500);
						Software.FileHandler.Log("Client", $"Asked to shutdown with result: {ClientUnavailable()}");
						CheckConnection();
						break;
				}
			
				Software.FileHandler.Log("Client", message);
			}
		}
		catch
		{
			Software.FileHandler.Log("Client", $"Read Interrupted, client unavailable: {ClientUnavailable()}");
			CheckConnection();
		}
	}

	private static bool Write(string message)
	{
		if(ClientUnavailable()) return false;

		try
		{
			_computerClient!.Send(Encoding.UTF8.GetBytes(message));
			return true;
		}
		catch
		{
			CheckConnection();
			return false;
		}
	}
	
	public static void Boot()
	{
		Helper.RunScript("wake-on-lan", NetworkAdapter.ComputerMac);
	}

	public static bool Shutdown()
	{
		return Write("shutdown");
	}
	
	public static bool Reboot()
	{
		return Write("reboot");
	}
	
	public static bool Notify(string text)
	{
		bool ready = Write("notify");
		return ready && Write(text);
	}
	
	public static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while ((Helper.Ping(NetworkAdapter.ComputerIp) || GetComputerStatus() == EPower.On) && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

		if ((DateTime.Now - start).Seconds < 3) await Task.Delay(20000);
		else await Task.Delay(5000);
	}
	
	private static void SendCommand(string command, bool asRoot, out string result)
	{
		var scriptPath = $"/home/{NetworkAdapter.DesktopUsername}/.config/cortana/cortana-script.sh";

		string usr = asRoot ? NetworkAdapter.DesktopRoot : NetworkAdapter.DesktopUsername;
		string pass = Software.FileHandler.Secrets.DesktopPassword;
		string addr = NetworkAdapter.ComputerIp;

		try
		{
			using var client = new SshClient(addr, usr, pass);
			client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
			client.Connect();

			string cmd = $"{scriptPath} {command}".Trim();
			SshCommand r = client.RunCommand(cmd);

			if (r.ExitStatus == 0) result = r.Result.Trim().Length > 0 && !r.Result.Trim().Equals("0") ? r.Result : "Command executed successfully\n";
			else result = r.Error.Trim().Length > 0 ? r.Error : "There was an error executing the command\n";
			result = result.Trim();

			var log = $"Exit Status: {r.ExitStatus}\nResult: {r.Result}Error: {r.Error}\n----\n";
			Software.FileHandler.Log("SSH", log);

			client.Disconnect();
		}
		catch
		{
			result = "Sorry, I couldn't send the command";
		}
	}

	private static void CheckConnection(object? sender, EventArgs e) => CheckConnection();
	private static void CheckConnection()
	{
		if (ClientUnavailable())
		{
			_computerClient?.Close();
			_computerClient = null;
			UpdateComputerStatus(EPower.Off);
		}
		else UpdateComputerStatus(EPower.On);
	}
	
	private static bool ClientUnavailable() => _computerClient is not { Connected: true };
	
	private static void UpdateComputerStatus(EPower power)
	{
		DeviceHandler.HardwareStates[EDevice.Computer] = power;
	}
	
	private static EPower GetComputerStatus()
	{
		return DeviceHandler.HardwareStates[EDevice.Computer];
	}
}
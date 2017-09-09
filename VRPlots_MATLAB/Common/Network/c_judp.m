classdef c_judp < handle
	% UDP class, loosely based on judp from Kevin Bartlett but many changes to allow keeping connection open between calls
	
	properties(SetAccess=protected)
		IP
		port
		canReceive
		receiveTimeout
		socket
	end
	
	methods
		function o = c_judp(varargin)
			
			p = inputParser();
			p.addParameter('IP','127.0.0.1',@ischar);
			p.addParameter('port',[],@isscalar);
			p.addParameter('canReceive',true,@islogical);
			p.addParameter('receiveTimeout',1e3,@isscalar); % in ms
			p.parse(varargin{:});
			s = p.Results;
			
			% copy parameters to class properties with the same names
			for iP = 1:length(p.Parameters)
				if isprop(o,p.Parameters{iP})
					o.(p.Parameters{iP}) = p.Results.(p.Parameters{iP});
				end
			end
			
			o.connect();
		end
		
		function isCon = isConnected(o)
			isCon = ~o.canReceive || ~isempty(o.socket);
		end
		
		function connect(o)
			if o.canReceive
				if o.isConnected()
					warning('Already connected.');
					return;
				end
				o.socket = java.net.DatagramSocket(o.port);
				o.socket.setSoTimeout(o.receiveTimeout);
				o.socket.setReuseAddress(1);
			else
				% no connections maintained when not receiving
			end
		end
		
		function delete(o) % destructor
			o.close();
		end
		
		function close(o)
			if ~isempty(o.socket)
				o.socket.close();
			end
		end
		
		function [bytesRead, originIP] = tryRead(o,varargin)
			p = inputParser();
			p.addParameter('maxNumBytes',[],@isscalar);
			p.addParameter('timeout',o.receiveTimeout,@isscalar); % in ms
			p.parse(varargin{:});
			s = p.Results;
			
			assert(o.canReceive);
			
			assert(o.isConnected());
			
			packet = java.net.DatagramPacket(zeros(1,s.maxNumBytes,'int8'),s.maxNumBytes);
			
			if s.timeout == 0
				% timeout values of zero are interpreted as infinity by socket.setSoTimeout, so make nonzero but small here
				s.timeout = 1;
			end
			
			if s.timeout ~= o.receiveTimeout
				o.socket.setSoTimeout(s.timeout);
			end
			
			try 
				o.socket.receive(packet);
			catch e
				if ~isempty(strfind(e.message,'java.net.SocketTimeoutException'))
					% timeout
					bytesRead = [];
					originIP = '';
					return;
				else
					rethrow(e);
				end
			end
				
			if s.timeout ~= o.receiveTimeout
				o.socket.setSoTimeout(s.timeout);
			end
			
			bytesRead = packet.getData;
			bytesRead = bytesRead(1:packet.getLength)';
			
			originIP = char(packet.getAddress.getHostAddress);
		end
		
		function sendBytes(o,bytesToSend)
			addr = java.net.InetAddress.getByName(o.IP);
			packet = java.net.DatagramPacket(bytesToSend,length(bytesToSend),addr,o.port);
			%TODO: maybe move initialization and closing of txSocket to one-time init/destruct to improve performance
			txSocket = java.net.DatagramSocket;
			txSocket.setReuseAddress(1);
			txSocket.send(packet);
			txSocket.close;
		end
	end
end
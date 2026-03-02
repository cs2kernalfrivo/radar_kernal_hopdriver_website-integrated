from django.shortcuts import render
from django.http import JsonResponse
from django.views.decorators.csrf import csrf_exempt
from asgiref.sync import async_to_sync
from channels.layers import get_channel_layer
import json

# This view displays the HTML page
def radar_view(request):
    return render(request, 'radar/radar.html')

# This is the API endpoint the C# script talks to
@csrf_exempt
def update_radar_api(request):
    if request.method == 'POST':
        try:
            # 1. Parse the incoming Packet: {"MapName": "de_dust2", "Players": [...]}
            radar_packet = json.loads(request.body)
            
            # 2. Get the Channel Layer (WebSocket system)
            channel_layer = get_channel_layer()

            # 3. Broadcast the packet to all connected browsers in the "radar" group
            async_to_sync(channel_layer.group_send)(
                "radar",
                {
                    "type": "radar.update", # This matches radar_update in consumers.py
                    "data": radar_packet,   # The whole packet (Map + Players)
                }
            )
            
            # 4. Tell C# everything went well
            return JsonResponse({"status": "success"})
            
        except Exception as e:
            print(f"CRITICAL ERROR IN VIEW: {e}")
            return JsonResponse({"status": "error", "message": str(e)}, status=500)
            
    return JsonResponse({"status": "error", "message": "Invalid method"}, status=400)
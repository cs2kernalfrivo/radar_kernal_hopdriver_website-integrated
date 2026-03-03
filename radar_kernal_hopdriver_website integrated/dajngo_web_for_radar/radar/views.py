from django.shortcuts import render
from django.http import JsonResponse
from django.views.decorators.csrf import csrf_exempt
from asgiref.sync import async_to_sync
from channels.layers import get_channel_layer
import json

def radar_view(request):
    return render(request, 'radar/radar.html')

@csrf_exempt
def update_radar_api(request):
    if request.method == 'POST':
        try:
            radar_packet = json.loads(request.body)
            channel_layer = get_channel_layer()
            async_to_sync(channel_layer.group_send)(
                "radar", {"type": "radar.update", "data": radar_packet}
            )
            return JsonResponse({"status": "success"})
        except Exception:
            return JsonResponse({"status": "error"}, status=500)
    return JsonResponse({"status": "error"}, status=400)
from django.urls import re_path
from . import consumers

# routing.py
websocket_urlpatterns = [
    re_path(r'ws/radar/$', consumers.RadarConsumer.as_asgi()),
    re_path(r'ws/esp/$', consumers.ESPConsumer.as_asgi()),
    
    # NEW: The "Input" for C#
    re_path(r'ws/inbound/$', consumers.InboundDataConsumer.as_asgi()),
]
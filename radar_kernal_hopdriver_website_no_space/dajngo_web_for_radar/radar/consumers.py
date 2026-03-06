import json
from channels.generic.websocket import AsyncWebsocketConsumer

class RadarConsumer(AsyncWebsocketConsumer):
    async def connect(self):
        await self.channel_layer.group_add("radar", self.channel_name)
        await self.accept()

    async def disconnect(self, close_code):
        await self.channel_layer.group_discard("radar", self.channel_name)

    async def radar_update(self, event):
        # Data is already a JSON string from InboundConsumer
        await self.send(text_data=event['data'])

# radar/consumers.py
import json
from channels.generic.websocket import AsyncWebsocketConsumer

class InboundDataConsumer(AsyncWebsocketConsumer):
    async def connect(self):
        await self.accept()

    async def receive(self, text_data):
        # This sends the raw C# string to the 'esp' and 'radar' groups
        await self.channel_layer.group_send(
            "esp", {
                "type": "esp.update",
                "data": text_data # This is the raw JSON string
            }
        )
        await self.channel_layer.group_send(
            "radar", {
                "type": "radar.update",
                "data": text_data
            }
        )

class ESPConsumer(AsyncWebsocketConsumer):
    async def connect(self):
        await self.channel_layer.group_add("esp", self.channel_name)
        await self.accept()

    async def disconnect(self, close_code):
        await self.channel_layer.group_discard("esp", self.channel_name)

    async def esp_update(self, event):
        # event['data'] is the JSON string sent from InboundDataConsumer
        await self.send(text_data=event['data'])
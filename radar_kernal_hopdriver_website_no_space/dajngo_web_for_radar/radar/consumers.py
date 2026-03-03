import json
from channels.generic.websocket import WebsocketConsumer
from asgiref.sync import async_to_sync

class RadarConsumer(WebsocketConsumer):
    def connect(self):
        # FIX: group_add is async, must use async_to_sync in a sync consumer
        async_to_sync(self.channel_layer.group_add)("radar", self.channel_name)
        self.accept()
        print(f"DEBUG: Browser connected and joined 'radar' group.")

    def disconnect(self, close_code):
        # FIX: group_discard is async
        async_to_sync(self.channel_layer.group_discard)("radar", self.channel_name)
        print("DEBUG: Browser disconnected.")

    # This method is called when the view sends a message to the group
    def radar_update(self, event):
        # print(f"DEBUG: Consumer received data for {len(event['data'])} players")
        # Send the received data down the WebSocket to the browser
        self.send(text_data=json.dumps(event['data']))
import os
from django.core.asgi import get_asgi_application
from channels.routing import ProtocolTypeRouter, URLRouter
from channels.auth import AuthMiddlewareStack
import radar.routing

os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'web_radar.settings')

application = ProtocolTypeRouter({
    "http": get_asgi_application(),
    "websocket": AuthMiddlewareStack(
        URLRouter(
            radar.routing.websocket_urlpatterns
        )
    ),
})
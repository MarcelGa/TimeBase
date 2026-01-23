"""Configuration loading utilities for TimeBase providers."""

import yaml
from typing import Dict, Any, List, Optional
from pathlib import Path
from dataclasses import dataclass, field


@dataclass
class ProviderConfig:
    """Provider configuration loaded from config.yaml"""
    
    name: str
    version: str
    slug: str
    description: str
    image: str
    arch: List[str]
    capabilities: Dict[str, bool]
    data_types: List[str]
    intervals: List[str]
    rate_limits: Dict[str, int]
    options: Dict[str, Any] = field(default_factory=dict)
    host_network: bool = False
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'ProviderConfig':
        """Create ProviderConfig from dictionary."""
        return cls(
            name=data['name'],
            version=data['version'],
            slug=data['slug'],
            description=data.get('description', ''),
            image=data.get('image', ''),
            arch=data.get('arch', ['amd64']),
            capabilities=data.get('capabilities', {}),
            data_types=data.get('data_types', []),
            intervals=data.get('intervals', []),
            rate_limits=data.get('rate_limits', {}),
            options=data.get('options', {}),
            host_network=data.get('host_network', False)
        )


def load_config(config_path: str = "config.yaml") -> ProviderConfig:
    """Load provider configuration from YAML file.
    
    Args:
        config_path: Path to the configuration file
        
    Returns:
        ProviderConfig object
        
    Raises:
        FileNotFoundError: If config file doesn't exist
        yaml.YAMLError: If config file is invalid YAML
        KeyError: If required fields are missing
    """
    path = Path(config_path)
    
    if not path.exists():
        raise FileNotFoundError(f"Configuration file not found: {config_path}")
    
    with open(path, 'r') as f:
        data = yaml.safe_load(f)
    
    if not data:
        raise ValueError(f"Configuration file is empty: {config_path}")
    
    # Validate required fields
    required_fields = ['name', 'version', 'slug']
    missing_fields = [field for field in required_fields if field not in data]
    if missing_fields:
        raise KeyError(f"Missing required configuration fields: {', '.join(missing_fields)}")
    
    return ProviderConfig.from_dict(data)
